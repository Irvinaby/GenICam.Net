using System.Net;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

/// <summary>
/// Fake UDP transport for unit testing GVCP components without real sockets.
/// </summary>
internal class FakeUdpTransport : IUdpTransport
{
    private readonly Queue<UdpReceiveResult> _receiveQueue = new();
    private readonly List<(byte[] Data, IPEndPoint EndPoint)> _sentPackets = [];
    private bool _broadcastEnabled;

    public IReadOnlyList<(byte[] Data, IPEndPoint EndPoint)> SentPackets => _sentPackets;
    public bool BroadcastEnabled => _broadcastEnabled;

    public void EnqueueReceive(byte[] data, IPEndPoint? remoteEndPoint = null)
    {
        remoteEndPoint ??= new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);
        _receiveQueue.Enqueue(new UdpReceiveResult(data, remoteEndPoint));
    }

    public Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        _sentPackets.Add((data, endPoint));
        return Task.CompletedTask;
    }

    public Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (_receiveQueue.Count > 0)
            return Task.FromResult(_receiveQueue.Dequeue());

        // Simulate timeout by waiting on the cancellation token
        var tcs = new TaskCompletionSource<UdpReceiveResult>();
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    public void EnableBroadcast() => _broadcastEnabled = true;

    public void Dispose() { }
}
