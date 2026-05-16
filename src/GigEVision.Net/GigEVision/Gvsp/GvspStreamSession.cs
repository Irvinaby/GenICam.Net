using System.Net;
using System.Net.Sockets;
using GenICam.Net.GigEVision.Gvcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Owns the UDP transport and GVSP receive loop for one stream channel.
/// </summary>
public sealed class GvspStreamSession : IDisposable
{
    private readonly ILogger<GvspStreamSession> _logger;
    private CountingUdpTransport? _streamTransport;
    private GvspReceiver? _receiver;
    private CancellationTokenSource? _cts;

    public GvspStreamSession(ILogger<GvspStreamSession>? logger = null)
    {
        _logger = logger ?? NullLogger<GvspStreamSession>.Instance;
    }

    public event EventHandler<GvspFrame>? FrameReceived;

    public event EventHandler<GvspPacketStats>? PacketStatsUpdated;

    public bool IsStreaming { get; private set; }

    public int LocalPort { get; private set; }

    public GvspPacketStats PacketStats => _streamTransport?.Stats ?? new GvspPacketStats(0, 0, 0, 0, 0);

    public int Start(int streamPort = 0)
    {
        if (IsStreaming)
        {
            _logger.LogWarning("Start called while already streaming");
            return 0;
        }

        _cts = new CancellationTokenSource();
        var udpClient = new UdpClient(streamPort);
        udpClient.Client.ReceiveBufferSize = 64 * 1024 * 1024;
        LocalPort = ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;

        _streamTransport = new CountingUdpTransport(new UdpTransportAdapter(udpClient), OnUdpPacketReceived);
        _receiver = new GvspReceiver(_streamTransport);
        _receiver.FrameReceived += OnFrameReceived;

        IsStreaming = true;
        _logger.LogInformation("GVSP receiver started on local port {Port}", LocalPort);
        _ = RunReceiveLoopAsync();
        return LocalPort;
    }

    public void Stop()
    {
        if (!IsStreaming)
            return;

        _logger.LogInformation("Stopping GVSP streaming");
        _cts?.Cancel();
        if (_receiver is not null)
        {
            _receiver.FrameReceived -= OnFrameReceived;
            _receiver.Dispose();
            _receiver = null;
        }

        _streamTransport = null;
        _cts?.Dispose();
        _cts = null;
        IsStreaming = false;
        LocalPort = 0;
    }

    private async Task RunReceiveLoopAsync()
    {
        try
        {
            await _receiver!.StartAsync(_cts!.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GVSP receive loop terminated with error");
        }
    }

    private void OnFrameReceived(object? sender, GvspFrame frame)
    {
        FrameReceived?.Invoke(this, frame);
    }

    private void OnUdpPacketReceived(GvspPacketStats stats, int packetLength)
    {
        if (stats.ReceivedPacketCount <= 5)
            _logger.LogDebug("GVSP UDP packet {PacketCount}: {Length} bytes", stats.ReceivedPacketCount, packetLength);

        PacketStatsUpdated?.Invoke(this, stats);
    }

    public void Dispose() => Stop();

    private sealed class CountingUdpTransport(IUdpTransport inner, Action<GvspPacketStats, int> packetReceived) : IUdpTransport
    {
        private int _receivedPacketCount;
        private int _leaderCount;
        private int _trailerCount;
        private int _payloadCount;
        private int _otherCount;

        public GvspPacketStats Stats => new(
            _receivedPacketCount,
            _leaderCount,
            _payloadCount,
            _trailerCount,
            _otherCount);

        public Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default)
            => inner.SendAsync(data, endPoint, cancellationToken);

        public async Task<GenICam.Net.GigEVision.Gvcp.UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            var result = await inner.ReceiveAsync(cancellationToken);
            _receivedPacketCount++;
            CountPacketType(result.Buffer);
            if (_receivedPacketCount <= 5 || _receivedPacketCount % 500 == 0)
                packetReceived(Stats, result.Buffer.Length);
            return result;
        }

        public void EnableBroadcast() => inner.EnableBroadcast();

        public void Dispose() => inner.Dispose();

        private void CountPacketType(byte[] packet)
        {
            if (packet.Length < GvspConstants.GenericHeaderSize)
            {
                _otherCount++;
                return;
            }

            switch ((GvspPacketType)packet[4])
            {
                case GvspPacketType.Leader:
                    _leaderCount++;
                    break;
                case GvspPacketType.Payload:
                    _payloadCount++;
                    break;
                case GvspPacketType.Trailer:
                    _trailerCount++;
                    break;
                default:
                    _otherCount++;
                    break;
            }
        }
    }
}
