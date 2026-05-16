using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using GenICam.Net.GenApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Owns the GVCP connection, GenICam node map, heartbeat, and acquisition setup for one GigE Vision camera.
/// </summary>
public sealed class GigECameraSession : IDisposable
{
    private const int HeartbeatTimeoutMs = 30000;
    private const int GvcpBusyRetryCount = 5;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GvcpBusyRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly ILogger<GigECameraSession> _logger;
    private readonly GvcpClient _client;
    private readonly GigECameraInfo _camera;
    private CancellationTokenSource? _heartbeatCts;

    private GigECameraSession(
        GigECameraInfo camera,
        GvcpClient client,
        NodeMap nodeMap,
        ILogger<GigECameraSession>? logger)
    {
        _camera = camera;
        _client = client;
        NodeMap = nodeMap;
        _logger = logger ?? NullLogger<GigECameraSession>.Instance;
    }

    public GigECameraInfo Camera => _camera;

    public NodeMap NodeMap { get; }

    public static async Task<GigECameraSession> ConnectAsync(
        GigECameraInfo camera,
        string? xmlSaveDirectory = null,
        ILogger<GigECameraSession>? logger = null,
        CancellationToken cancellationToken = default)
    {
        logger ??= NullLogger<GigECameraSession>.Instance;

        var transport = new UdpTransportAdapter();
        var client = new GvcpClient(transport, new IPEndPoint(camera.IpAddress, GvcpConstants.Port));

        try
        {
            logger.LogInformation("Connecting GVCP session to {IpAddress}", camera.IpAddress);
            await TakeControlAsync(client, logger, cancellationToken);
            await ConfigureBootstrapHeartbeatAsync(client, logger, cancellationToken);

            xmlSaveDirectory ??= Path.Combine(AppContext.BaseDirectory, "camera-xml");
            var nodeMap = await GvcpXmlLoader.LoadNodeMapAsync(client, logger, xmlSaveDirectory);
            nodeMap.Connect(new GigEPort(client));

            var session = new GigECameraSession(camera, client, nodeMap, logger);
            await Task.Run(() => session.PrefetchNodeValues(), cancellationToken);
            return session;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task StartAcquisitionAsync(int localPort, CancellationToken cancellationToken = default)
    {
        var localIp = GetLocalIpForCamera(_camera.IpAddress);
        StopHeartbeat();

        await StopCameraAcquisitionForSetupAsync(cancellationToken);
        await ConfigureHeartbeatAsync(cancellationToken);
        ConfigureAcquisitionDefaults();
        await Task.Delay(250, cancellationToken);
        await ConfigureStreamAsync(localIp, localPort, cancellationToken);

        if (NodeMap.GetNode("AcquisitionStart") is ICommand startCommand)
        {
            startCommand.Execute();
            _logger.LogInformation("AcquisitionStart command executed");
        }
        else
        {
            _logger.LogWarning("AcquisitionStart node not found in node map");
        }

        StartHeartbeat();
    }

    public async Task StopAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        StopHeartbeat();

        if (NodeMap.GetNode("AcquisitionStop") is ICommand stopCommand)
        {
            try
            {
                await ExecuteCommandWithBusyRetryAsync(
                    stopCommand,
                    "AcquisitionStop",
                    cancellationToken);
                _logger.LogInformation("AcquisitionStop command executed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AcquisitionStop command failed");
            }
        }

        try
        {
            await WriteRegisterWithBusyRetryAsync(
                _client,
                GvcpConstants.Scp0Register,
                0,
                _logger,
                cancellationToken);
            _logger.LogDebug("Stream channel disabled (SCP0 = 0)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable stream channel");
        }
    }

    private static async Task TakeControlAsync(
        GvcpClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.WriteRegisterAsync(GvcpConstants.CcpRegister, 2, cancellationToken);
            logger.LogDebug("Exclusive control acquired (CCP register)");
        }
        catch (GvcpException ex)
        {
            logger.LogWarning(ex, "Exclusive control failed; trying control privilege");
            await client.WriteRegisterAsync(GvcpConstants.CcpRegister, 1, cancellationToken);
            logger.LogDebug("Control privilege acquired (CCP register)");
        }
    }

    private async Task ConfigureHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (TrySetIntegerNode("GevHeartbeatTimeout", HeartbeatTimeoutMs) ||
            TrySetIntegerNode("DeviceLinkHeartbeatTimeout", HeartbeatTimeoutMs))
        {
            return;
        }

        await ConfigureBootstrapHeartbeatAsync(_client, _logger, cancellationToken);
    }

    private static async Task ConfigureBootstrapHeartbeatAsync(
        GvcpClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteRegisterWithBusyRetryAsync(
                client,
                GvcpConstants.HeartbeatTimeoutRegister,
                HeartbeatTimeoutMs,
                logger,
                cancellationToken);
            logger.LogInformation("Set GVCP heartbeat timeout register to {HeartbeatTimeoutMs} ms", HeartbeatTimeoutMs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not set GVCP heartbeat timeout; continuing with periodic heartbeat reads");
        }
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        var token = _heartbeatCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            var consecutiveFailures = 0;

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    try
                    {
                        await _client.ReadRegisterAsync(GvcpConstants.CcpRegister, token);
                        consecutiveFailures = 0;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (TimeoutException)
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures == 1 || consecutiveFailures % 5 == 0)
                        {
                            _logger.LogDebug(
                                "GVCP heartbeat timed out during acquisition; consecutiveFailures={ConsecutiveFailures}",
                                consecutiveFailures);
                        }
                    }
                    catch (GvcpException ex)
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures == 1 || consecutiveFailures % 5 == 0)
                        {
                            _logger.LogDebug(
                                "GVCP heartbeat read failed during acquisition; status={Status}, consecutiveFailures={ConsecutiveFailures}",
                                ex.Status,
                                consecutiveFailures);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when acquisition stops.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GVCP heartbeat loop stopped unexpectedly");
            }
        }, token);
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCts is null)
            return;

        _heartbeatCts.Cancel();
        _heartbeatCts.Dispose();
        _heartbeatCts = null;
    }

    private async Task ConfigureStreamAsync(
        IPAddress localIp,
        int localPort,
        CancellationToken cancellationToken)
    {
        var ipBytes = localIp.GetAddressBytes();
        var ipValue = BinaryPrimitives.ReadUInt32BigEndian(ipBytes);

        _logger.LogInformation(
            "Configuring stream: localIp={LocalIp}, port={Port}, destination=0x{Destination:X8}",
            localIp,
            localPort,
            ipValue);

        await WriteRegisterWithBusyRetryAsync(_client, GvcpConstants.Scp0Register, (uint)localPort, _logger, cancellationToken);
        await WriteRegisterWithBusyRetryAsync(_client, GvcpConstants.Scps0Register, 1500, _logger, cancellationToken);
        TrySetIntegerNode("GevSCPD", 1000);

        if (!TrySetIntegerNode("GevSCDA", ipValue))
        {
            try
            {
                await WriteRegisterWithBusyRetryAsync(_client, GvcpConstants.Scda0Register, ipValue, _logger, cancellationToken);
                _logger.LogDebug("Stream destination configured: SCDA0={IpValue:X8}", ipValue);
            }
            catch (GvcpException ex) when (ex.Status == GvcpStatus.WriteProtect)
            {
                _logger.LogInformation("Stream destination address is write-protected; keeping the camera's current destination address");
            }
        }
    }

    private async Task StopCameraAcquisitionForSetupAsync(CancellationToken cancellationToken)
    {
        if (NodeMap.GetNode("AcquisitionStop") is not ICommand stopCommand)
            return;

        try
        {
            await ExecuteCommandWithBusyRetryAsync(
                stopCommand,
                "AcquisitionStop before stream setup",
                cancellationToken);
            _logger.LogInformation("AcquisitionStop command executed before stream setup");
            await Task.Delay(250, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AcquisitionStop before stream setup failed; continuing with stream setup");
        }
    }

    private async Task ExecuteCommandWithBusyRetryAsync(
        ICommand command,
        string commandName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                command.Execute();
                return;
            }
            catch (GvcpException ex) when (ex.Status == GvcpStatus.Busy && attempt <= GvcpBusyRetryCount)
            {
                _logger.LogDebug(
                    ex,
                    "{CommandName} busy; retrying attempt={Attempt}/{MaxAttempts}",
                    commandName,
                    attempt,
                    GvcpBusyRetryCount);
                await Task.Delay(GvcpBusyRetryDelay, cancellationToken);
            }
        }
    }

    private static async Task WriteRegisterWithBusyRetryAsync(
        GvcpClient client,
        uint address,
        uint value,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await client.WriteRegisterAsync(address, value, cancellationToken);
                return;
            }
            catch (GvcpException ex) when (ex.Status == GvcpStatus.Busy && attempt <= GvcpBusyRetryCount)
            {
                logger.LogDebug(
                    ex,
                    "GVCP register write busy; retrying address=0x{Address:X8}, attempt={Attempt}/{MaxAttempts}",
                    address,
                    attempt,
                    GvcpBusyRetryCount);
                await Task.Delay(GvcpBusyRetryDelay, cancellationToken);
            }
        }
    }

    private void ConfigureAcquisitionDefaults()
    {
        TrySetEnumerationNode("AcquisitionMode", "Continuous");
        TrySetEnumerationNode("TriggerSelector", "FrameStart");
        TrySetEnumerationNode("TriggerMode", "Off");
        TrySetEnumerationNode("ExposureAuto", "Continuous", "Once");
        TrySetEnumerationNode("GainAuto", "Continuous", "Once");
        TrySetIntegerNode("AcquisitionFrameCount", 10_000);
    }

    private bool TrySetIntegerNode(string nodeName, long value)
    {
        try
        {
            if (NodeMap.GetNode(nodeName) is not IInteger node)
                return false;

            if (!IsWritable(node.AccessMode))
            {
                _logger.LogInformation("Skipping {NodeName}; access mode is {AccessMode}", nodeName, node.AccessMode);
                return false;
            }

            node.Value = ClampToIncrement(value, node);
            _logger.LogInformation("Set {NodeName}={Value}", nodeName, SafeReadInteger(node));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set integer node {NodeName}", nodeName);
            return false;
        }
    }

    private bool TrySetEnumerationNode(string nodeName, params string[] values)
    {
        try
        {
            if (NodeMap.GetNode(nodeName) is not IEnumeration node)
                return false;

            if (!IsWritable(node.AccessMode))
            {
                _logger.LogInformation("Skipping {NodeName}; access mode is {AccessMode}", nodeName, node.AccessMode);
                return false;
            }

            var entry = values
                .Select(value => node.GetEntryByName(value) ?? node.Entries.FirstOrDefault(e => string.Equals(e.Symbolic, value, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(e => e is not null);
            if (entry is null)
            {
                _logger.LogInformation(
                    "Could not set {NodeName}; none of [{Values}] exist. Available: {Entries}",
                    nodeName,
                    string.Join(", ", values),
                    string.Join(", ", node.Entries.Select(e => e.Symbolic)));
                return false;
            }

            node.Value = entry.Symbolic;
            _logger.LogInformation("Set {NodeName}={Value}", nodeName, SafeReadEnumeration(node));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set enumeration node {NodeName}", nodeName);
            return false;
        }
    }

    private void PrefetchNodeValues()
    {
        var count = 0;
        var skipped = 0;
        var unreadable = 0;
        foreach (var node in NodeMap.Nodes)
        {
            if (!IsReadable(node.AccessMode))
            {
                skipped++;
                continue;
            }

            try
            {
                _ = node switch
                {
                    IInteger i => i.Value.ToString(),
                    IFloat f => f.Value.ToString(),
                    IBoolean b => b.Value.ToString(),
                    IString s => s.Value,
                    IEnumeration e => e.Value,
                    _ => null
                };
                count++;
            }
            catch (Exception ex)
            {
                if (ex is GvcpException)
                {
                    unreadable++;
                    _logger.LogTrace(ex, "Prefetch skipped unreadable device node {Name}", node.Name);
                }
                else
                {
                    _logger.LogDebug(ex, "Prefetch failed for node {Name}", node.Name);
                }
            }
        }

        _logger.LogInformation(
            "Prefetched values for {Count} nodes; skipped {Skipped} non-readable nodes and {Unreadable} device-rejected nodes",
            count,
            skipped,
            unreadable);
    }

    private static bool IsWritable(AccessMode accessMode) => accessMode is AccessMode.RW or AccessMode.WO;

    private static bool IsReadable(AccessMode accessMode) => accessMode is AccessMode.RO or AccessMode.RW;

    private static string SafeReadInteger(IInteger node)
    {
        try
        {
            return node.AccessMode == AccessMode.WO ? "<write-only>" : node.Value.ToString();
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private static string SafeReadEnumeration(IEnumeration node)
    {
        try
        {
            return node.AccessMode == AccessMode.WO ? "<write-only>" : node.Value;
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private static long ClampToIncrement(long value, IInteger node)
    {
        var clamped = Math.Min(Math.Max(value, node.Min), node.Max);
        if (node.Increment <= 1)
            return clamped;

        return node.Min + ((clamped - node.Min) / node.Increment) * node.Increment;
    }

    private static IPAddress GetLocalIpForCamera(IPAddress cameraIp)
    {
        using var probe = new UdpClient();
        probe.Connect(cameraIp, GvcpConstants.Port);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Address;
    }

    public void Dispose()
    {
        StopHeartbeat();
        _client.Dispose();
    }
}
