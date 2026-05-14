using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Root view-model. Owns the camera list, node tree, and stream sub-view-models.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int DefaultStreamPort = 50000;
    private const int HeartbeatTimeoutMs = 30000;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private string _title = "CameraViewer";

    [ObservableProperty]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    public CameraViewModel CameraVm { get; }
    public NodeTreeViewModel NodeTreeVm { get; }
    public StreamViewModel StreamVm { get; }

    private GvcpClient? _gvcpClient;
    private GigECameraInfo? _connectedCamera;
    private NodeMap? _nodeMap;
    private CancellationTokenSource? _heartbeatCts;

    public MainViewModel(Dispatcher dispatcher, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MainViewModel>();
        CameraVm = new CameraViewModel(loggerFactory.CreateLogger<CameraViewModel>());
        NodeTreeVm = new NodeTreeViewModel();
        StreamVm = new StreamViewModel(dispatcher, loggerFactory.CreateLogger<StreamViewModel>());
        StreamVm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StreamViewModel.IsStreaming))
            {
                StartAcquisitionCommand.NotifyCanExecuteChanged();
                StopAcquisitionCommand.NotifyCanExecuteChanged();
            }
        };

        CameraVm.CameraConnectRequested += OnCameraConnectRequested;
        _logger.LogInformation("MainViewModel initialized");
    }

    private async void OnCameraConnectRequested(object? sender, GigECameraInfo cam)
    {
        try
        {
            await ConnectToCameraAsync(cam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to camera at {IpAddress}", cam.IpAddress);
            ConnectionStatus = $"Connection failed: {ex.Message}";
            StopHeartbeat();
        }
    }

    private async Task ConnectToCameraAsync(GigECameraInfo cam)
    {
        _logger.LogInformation("Connecting to camera at {IpAddress}", cam.IpAddress);
        ConnectionStatus = $"Connecting to {cam.IpAddress}…";
        IsCameraConnected = false;

        // Close any previous GVCP connection
        StopHeartbeat();
        _gvcpClient?.Dispose();
        _gvcpClient = null;
        _connectedCamera = cam;

        // Establish GVCP connection to the camera
        var transport = new UdpTransportAdapter();
        _gvcpClient = new GvcpClient(transport, new IPEndPoint(cam.IpAddress, GvcpConstants.Port));
        _logger.LogDebug("GVCP client created for {IpAddress}:{Port}", cam.IpAddress, GvcpConstants.Port);

        // Try to load the camera's actual XML description from the device
        ConnectionStatus = $"Loading camera XML from {cam.IpAddress}…";
        _nodeMap = await LoadCameraNodeMapAsync();

        if (_nodeMap is null)
        {
            _logger.LogError("Could not load camera XML from device; connection aborted");
            ConnectionStatus = $"Failed to load camera XML from {cam.IpAddress}";
            return;
        }

        // Connect the node map to the GigE Vision port so commands and registers reach the camera
        var port = new GigEPort(_gvcpClient);
        _nodeMap.Connect(port);
        _logger.LogInformation("Node map connected to GigE port for {Model} ({IpAddress})", cam.ModelName, cam.IpAddress);

        // Pre-cache all register-backed node values so expanding the tree doesn't block
        ConnectionStatus = $"Reading node values from {cam.IpAddress}…";
        await Task.Run(() => PrefetchNodeValues(_nodeMap));

        // Load the tree after port is connected and values are cached
        NodeTreeVm.Load(_nodeMap);

        Title = $"CameraViewer — {cam.ManufacturerName} {cam.ModelName} ({cam.IpAddress})";
        ConnectionStatus = $"Connected: {cam.ManufacturerName} {cam.ModelName}";
        IsCameraConnected = true;
    }

    [RelayCommand(CanExecute = nameof(CanStartAcquisition))]
    private async Task StartAcquisitionAsync()
    {
        _logger.LogInformation("Starting acquisition");
        var localPort = StreamVm.StartStreaming(DefaultStreamPort);
        if (localPort == 0)
        {
            _logger.LogWarning("StartStreaming returned port 0; aborting acquisition start");
            return;
        }

        if (_gvcpClient is not null && _connectedCamera is not null)
        {
            try
            {
                // Determine which local IP reaches the camera
                var localIp = GetLocalIpForCamera(_connectedCamera.IpAddress);
                _logger.LogDebug("Local IP for camera: {LocalIp}", localIp);

                await TakeControlAsync(_gvcpClient);
                await ConfigureHeartbeatAsync(_gvcpClient, _nodeMap);
                StartHeartbeat(_gvcpClient);
                ConfigureAcquisitionDefaults(_nodeMap);
                await ConfigureStreamAsync(_gvcpClient, _nodeMap, localIp, localPort);

                // Execute AcquisitionStart command to tell the camera to begin streaming
                var startCmd = _nodeMap?.GetNode("AcquisitionStart") as ICommand;
                if (startCmd is not null)
                {
                    startCmd.Execute();
                    _logger.LogInformation("AcquisitionStart command executed");
                }
                else
                {
                    _logger.LogWarning("AcquisitionStart node not found in node map");
                }

                ConnectionStatus = $"Streaming on port {localPort}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream setup failed");
                ConnectionStatus = $"Stream setup failed: {ex.Message}";
                StopHeartbeat();
                StreamVm.StopStreaming();
            }
        }
        else
        {
            _logger.LogDebug("No GVCP client or camera; GVSP receiver is listening but no camera to stream from");
        }

        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStopAcquisition))]
    private async Task StopAcquisitionAsync()
    {
        _logger.LogInformation("Stopping acquisition");
        StopHeartbeat();

        // Execute AcquisitionStop command to tell the camera to stop streaming
        var stopCmd = _nodeMap?.GetNode("AcquisitionStop") as ICommand;
        if (stopCmd is not null)
        {
            try
            {
                stopCmd.Execute();
                _logger.LogInformation("AcquisitionStop command executed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AcquisitionStop command failed");
            }
        }

        if (_gvcpClient is not null)
        {
            try
            {
                // Disable stream channel 0
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scp0Register, 0);
                _logger.LogDebug("Stream channel disabled (SCP0 = 0)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable stream channel");
            }
        }

        StreamVm.StopStreaming();
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Loads the camera's GenICam XML description from the device via GVCP bootstrap registers.
    /// </summary>
    private async Task<NodeMap?> LoadCameraNodeMapAsync()
    {
        if (_gvcpClient is null) return null;

        try
        {
            // Read the First URL register (0x0200, 512 bytes)
            var urlBytes = await ReadLargeBlockAsync(_gvcpClient, GvcpConstants.FirstUrlRegister, GvcpConstants.UrlRegisterLength);
            var urlString = Encoding.ASCII.GetString(urlBytes).TrimEnd('\0');

            _logger.LogInformation("Camera XML URL: {Url}", urlString);

            if (!urlString.StartsWith("Local:", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unsupported XML URL scheme: {Url}", urlString);
                return null;
            }

            // Parse "Local:<filename>;hex_address;hex_size"
            var parts = urlString[6..].Split(';');
            if (parts.Length < 3)
            {
                _logger.LogWarning("Invalid XML URL format: {Url}", urlString);
                return null;
            }

            var filename = parts[0];
            var xmlAddress = ParseHex(parts[1]);
            var xmlSize = (int)ParseHex(parts[2]);

            _logger.LogInformation("Reading camera XML: file={Filename}, address=0x{Address:X}, size={Size}",
                filename, xmlAddress, xmlSize);

            // Read the XML data from the camera memory
            var xmlData = await ReadLargeBlockAsync(_gvcpClient, xmlAddress, xmlSize);

            // Decompress if the file is a ZIP archive
            string xmlContent;
            if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var memStream = new MemoryStream(xmlData);
                using var archive = new ZipArchive(memStream, ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault();
                if (entry is null)
                {
                    _logger.LogWarning("Camera XML ZIP archive is empty");
                    return null;
                }
                using var reader = new StreamReader(entry.Open());
                xmlContent = await reader.ReadToEndAsync();
                _logger.LogDebug("Decompressed XML from ZIP entry: {EntryName} ({Length} chars)", entry.Name, xmlContent.Length);
            }
            else
            {
                xmlContent = Encoding.UTF8.GetString(xmlData);
            }

            var nodeMap = NodeMapParser.Parse(xmlContent);
            _logger.LogInformation("Camera XML loaded: {NodeCount} nodes parsed", nodeMap.Nodes.Count);
            return nodeMap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load camera XML from device");
            return null;
        }
    }

    private static async Task<byte[]> ReadLargeBlockAsync(GvcpClient client, uint address, int length)
    {
        var result = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var chunkSize = Math.Min(length - offset, GvcpConstants.MaxBlockSize);
            var chunk = await client.ReadMemoryAsync(address + (uint)offset, chunkSize);
            chunk.CopyTo(result, offset);
            offset += chunkSize;
        }

        return result;
    }

    private async Task TakeControlAsync(GvcpClient client)
    {
        try
        {
            await client.WriteRegisterAsync(GvcpConstants.CcpRegister, 2);
            _logger.LogDebug("Exclusive control acquired (CCP register)");
        }
        catch (GvcpException ex)
        {
            _logger.LogWarning(ex, "Exclusive control failed; trying control privilege");
            await client.WriteRegisterAsync(GvcpConstants.CcpRegister, 1);
            _logger.LogDebug("Control privilege acquired (CCP register)");
        }
    }

    private async Task ConfigureHeartbeatAsync(GvcpClient client, NodeMap? nodeMap)
    {
        if (TrySetIntegerNode(nodeMap, "GevHeartbeatTimeout", HeartbeatTimeoutMs) ||
            TrySetIntegerNode(nodeMap, "DeviceLinkHeartbeatTimeout", HeartbeatTimeoutMs))
        {
            return;
        }

        try
        {
            await client.WriteRegisterAsync(GvcpConstants.HeartbeatTimeoutRegister, HeartbeatTimeoutMs);
            _logger.LogInformation("Set GVCP heartbeat timeout register to {HeartbeatTimeoutMs} ms", HeartbeatTimeoutMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set GVCP heartbeat timeout; continuing with periodic heartbeat reads");
        }
    }

    private void StartHeartbeat(GvcpClient client)
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        var token = _heartbeatCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await client.ReadRegisterAsync(GvcpConstants.CcpRegister, token);
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

    private async Task ConfigureStreamAsync(GvcpClient client, NodeMap? nodeMap, IPAddress localIp, int localPort)
    {
        var ipBytes = localIp.GetAddressBytes();
        var ipValue = BinaryPrimitives.ReadUInt32BigEndian(ipBytes);

        _logger.LogInformation("Configuring stream: localIp={LocalIp}, port={Port}, destination=0x{Destination:X8}", localIp, localPort, ipValue);

        TrySetIntegerNode(nodeMap, "GevSCPHostPort", localPort);
        TrySetIntegerNode(nodeMap, "GevSCPSPacketSize", 1500);

        if (!TrySetIntegerNode(nodeMap, "GevSCDA", ipValue))
        {
            try
            {
                await client.WriteRegisterAsync(GvcpConstants.Scda0Register, ipValue);
                _logger.LogDebug("Stream destination configured: SCDA0={IpValue:X8}", ipValue);
            }
            catch (GvcpException ex) when (ex.Status == GvcpStatus.WriteProtect)
            {
                _logger.LogInformation("Stream destination address is write-protected; keeping the camera's current destination address");
            }
        }

        await client.WriteRegisterAsync(GvcpConstants.Scp0Register, (uint)localPort);
        await client.WriteRegisterAsync(GvcpConstants.Scps0Register, 1500);
        _logger.LogDebug("Stream channel registers configured: SCP0={Port}, SCPS0=1500", localPort);
    }

    private void ConfigureAcquisitionDefaults(NodeMap? nodeMap)
    {
        TrySetEnumerationNode(nodeMap, "AcquisitionMode", "Continuous");
        TrySetEnumerationNode(nodeMap, "TriggerSelector", "FrameStart");
        TrySetEnumerationNode(nodeMap, "TriggerMode", "Off");
        TrySetEnumerationNode(nodeMap, "ExposureAuto", "Continuous", "Once");
        TrySetEnumerationNode(nodeMap, "GainAuto", "Continuous", "Once");
        TrySetIntegerNode(nodeMap, "AcquisitionFrameCount", 10_000);
        TrySetIntegerNode(nodeMap, "AcquisitionBurstFrameCount", 10_000);
    }

    private bool TrySetIntegerNode(NodeMap? nodeMap, string nodeName, long value)
    {
        try
        {
            if (nodeMap?.GetNode(nodeName) is not IInteger node)
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

    private bool TrySetEnumerationNode(NodeMap? nodeMap, string nodeName, params string[] values)
    {
        try
        {
            if (nodeMap?.GetNode(nodeName) is not IEnumeration node)
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
                _logger.LogInformation("Could not set {NodeName}; none of [{Values}] exist. Available: {Entries}",
                    nodeName, string.Join(", ", values), string.Join(", ", node.Entries.Select(e => e.Symbolic)));
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

    private static bool IsWritable(AccessMode accessMode) => accessMode is AccessMode.RW or AccessMode.WO;

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

    private static uint ParseHex(string hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        return Convert.ToUInt32(hex, 16);
    }

    /// <summary>
    /// Pre-reads all register-backed node values so they are cached and the UI doesn't block.
    /// Runs on a background thread.
    /// </summary>
    private void PrefetchNodeValues(NodeMap nodeMap)
    {
        var count = 0;
        foreach (var node in nodeMap.Nodes)
        {
            try
            {
                // Simply reading the value triggers the register read and populates the cache
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
                _logger.LogDebug(ex, "Prefetch failed for node {Name}", node.Name);
            }
        }
        _logger.LogInformation("Prefetched values for {Count} nodes", count);
    }

    private static IPAddress GetLocalIpForCamera(IPAddress cameraIp)
    {
        using var probe = new UdpClient();
        probe.Connect(cameraIp, GvcpConstants.Port);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Address;
    }

    private bool CanStopAcquisition() => IsCameraConnected && StreamVm.IsStreaming;
    private bool CanStartAcquisition() => IsCameraConnected && !StreamVm.IsStreaming;

    partial void OnIsCameraConnectedChanged(bool value)
    {
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }
}
