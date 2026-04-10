using System.Net;

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
    private ushort _requestId;

    /// <summary>
    /// Creates a new discovery instance using the given UDP transport.
    /// </summary>
    /// <param name="transport">UDP transport (must support broadcast).</param>
    public GigEDiscovery(IUdpTransport transport)
    {
        _transport = transport;
        _transport.EnableBroadcast();
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
                    continue;

                var ackHeader = GvcpAckHeader.FromBytes(result.Buffer);
                if (ackHeader.Acknowledge != GvcpCommandType.DiscoveryAck)
                    continue;
                if (ackHeader.Status != GvcpStatus.Success)
                    continue;

                var info = GvcpPackets.ParseDiscoveryAck(result.Buffer);
                cameras.Add(info);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached — return what we have
        }

        return cameras.AsReadOnly();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
