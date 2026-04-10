using System.Net;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Abstraction over UDP socket operations to enable unit testing of GVCP without real network access.
/// </summary>
public interface IUdpTransport : IDisposable
{
    /// <summary>
    /// Sends a UDP datagram to the specified endpoint.
    /// </summary>
    /// <param name="data">The datagram payload.</param>
    /// <param name="endPoint">The target endpoint (IP address and port).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a UDP datagram. Blocks until data arrives or the cancellation token fires.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (used for timeouts).</param>
    /// <returns>The received datagram payload and the remote endpoint it came from.</returns>
    Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables broadcast sending on this transport (required for discovery).
    /// </summary>
    void EnableBroadcast();
}

/// <summary>
/// Result of a UDP receive operation.
/// </summary>
public readonly struct UdpReceiveResult
{
    /// <summary>The received data.</summary>
    public byte[] Buffer { get; }

    /// <summary>The remote endpoint that sent the data.</summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>Creates a new receive result.</summary>
    public UdpReceiveResult(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        Buffer = buffer;
        RemoteEndPoint = remoteEndPoint;
    }
}
