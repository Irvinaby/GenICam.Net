using System.Net;
using System.Net.Sockets;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Real <see cref="IUdpTransport"/> implementation backed by a <see cref="UdpClient"/>.
/// Use this when communicating with physical GigE Vision devices.
/// </summary>
/// <remarks>
/// <para><b>Usage:</b></para>
/// <code>
/// using var transport = new UdpTransportAdapter();
/// using var discovery = new GigEDiscovery(transport);
/// var cameras = await discovery.DiscoverAsync(timeoutMs: 2000);
/// </code>
/// </remarks>
public sealed class UdpTransportAdapter : IUdpTransport
{
    private readonly UdpClient _client;
    private bool _disposed;

    /// <summary>
    /// Creates a new adapter with a fresh unbound <see cref="UdpClient"/>.
    /// </summary>
    public UdpTransportAdapter() : this(new UdpClient()) { }

    /// <summary>
    /// Creates a new adapter wrapping the given <see cref="UdpClient"/>.
    /// </summary>
    /// <param name="client">An existing UDP client to wrap. Ownership is transferred.</param>
    public UdpTransportAdapter(UdpClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client.SendAsync(data, data.Length, endPoint).WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = await _client.ReceiveAsync(cancellationToken);
        return new UdpReceiveResult(result.Buffer, result.RemoteEndPoint);
    }

    /// <inheritdoc/>
    public void EnableBroadcast()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _client.EnableBroadcast = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }
}
