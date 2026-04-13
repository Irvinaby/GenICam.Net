using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Discovers GigE Vision cameras on the local network by broadcasting GVCP discovery commands.
/// </summary>
/// <remarks>
/// <para><b>Usage:</b></para>
/// <code>
/// using var discovery = new GigEDiscovery(transport);
/// var cameras = await discovery.DiscoverAsync(timeoutMs: 2000);
/// foreach (var cam in cameras)
///     Console.WriteLine($"{cam.ManufacturerName} {cam.ModelName} at {cam.IpAddress}");
/// </code>
/// </remarks>
public class GigEDiscovery : IDisposable
{
    private readonly IUdpTransport _transport;
    private readonly ILogger<GigEDiscovery> _logger;
    private ushort _requestId;

    /// <summary>
    /// Creates a new discovery instance using the given UDP transport.
    /// </summary>
    /// <param name="transport">UDP transport (must support broadcast).</param>
    /// <param name="logger">Optional logger instance.</param>
    public GigEDiscovery(IUdpTransport transport, ILogger<GigEDiscovery>? logger = null)
    {
        _transport = transport;
        _transport.EnableBroadcast();
        _logger = logger ?? NullLogger<GigEDiscovery>.Instance;
    }

    /// <summary>
    /// Broadcasts a discovery command and collects all camera responses within the timeout window.
    /// </summary>
    /// <param name="timeoutMs">How long to listen for responses in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered cameras.</returns>
    public async Task<IReadOnlyList<GigECameraInfo>> DiscoverAsync(
        int timeoutMs = GvcpConstants.DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Broadcasting discovery command (timeout={TimeoutMs}ms)", timeoutMs);
        var reqId = ++_requestId;
        var packet = GvcpPackets.BuildDiscoveryCmd(reqId);
        var broadcastEp = new IPEndPoint(IPAddress.Broadcast, GvcpConstants.Port);

        await _transport.SendAsync(packet, broadcastEp, cancellationToken);

        var cameras = new List<GigECameraInfo>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await _transport.ReceiveAsync(cts.Token);

                if (result.Buffer.Length < GvcpConstants.AckHeaderSize)
                {
                    _logger.LogDebug("Discovery: ignoring short packet ({Length} bytes)", result.Buffer.Length);
                    continue;
                }

                var ackHeader = GvcpAckHeader.FromBytes(result.Buffer);
                if (ackHeader.Acknowledge != GvcpCommandType.DiscoveryAck)
                    continue;
                if (ackHeader.Status != GvcpStatus.Success)
                {
                    _logger.LogWarning("Discovery ACK with non-success status: {Status}", ackHeader.Status);
                    continue;
                }

                var info = GvcpPackets.ParseDiscoveryAck(result.Buffer);
                _logger.LogInformation("Discovered camera: {Vendor} {Model} at {IpAddress}", info.ManufacturerName, info.ModelName, info.IpAddress);
                cameras.Add(info);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached — return what we have
        }

        _logger.LogInformation("Discovery complete: {Count} camera(s) found", cameras.Count);
        return cameras.AsReadOnly();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
