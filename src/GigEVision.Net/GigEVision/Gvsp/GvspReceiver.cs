using GenICam.Net.GigEVision.Gvcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Receives GVSP packets over UDP and reassembles them into complete image frames.
/// Raises <see cref="FrameReceived"/> when a full frame (leader + payload + trailer) is assembled.
/// </summary>
/// <remarks>
/// <para><b>Usage:</b></para>
/// <code>
/// var receiver = new GvspReceiver(udpTransport);
/// receiver.FrameReceived += (sender, frame) =&gt;
///     Console.WriteLine($"Frame {frame.FrameId}: {frame.SizeX}x{frame.SizeY}");
/// await receiver.StartAsync(cancellationToken);
/// </code>
/// </remarks>
public class GvspReceiver : IDisposable
{
    private readonly IUdpTransport _transport;
    private readonly ILogger<GvspReceiver> _logger;
    private readonly Dictionary<ushort, FrameAssembly> _pendingFrames = new();

    /// <summary>Raised when a complete frame has been reassembled.</summary>
    public event EventHandler<GvspFrame>? FrameReceived;

    /// <summary>
    /// Creates a new GVSP receiver.
    /// </summary>
    /// <param name="transport">UDP transport for receiving stream packets.</param>
    /// <param name="logger">Optional logger instance.</param>
    public GvspReceiver(IUdpTransport transport, ILogger<GvspReceiver>? logger = null)
    {
        _transport = transport;
        _logger = logger ?? NullLogger<GvspReceiver>.Instance;
    }

    /// <summary>
    /// Starts receiving and reassembling GVSP packets. Runs until cancelled.
    /// </summary>
    /// <param name="cancellationToken">Token to stop receiving.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GVSP receive loop started");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _transport.ReceiveAsync(cancellationToken);
                ProcessPacket(result.Buffer);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GVSP receive loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving GVSP packet");
            }
        }
    }

    /// <summary>
    /// Processes a single GVSP packet. Can be called directly for testing without async receive loop.
    /// </summary>
    internal void ProcessPacket(byte[] packetData)
    {
        if (packetData.Length < GvspConstants.GenericHeaderSize)
        {
            _logger.LogWarning("Packet too small ({Length} bytes), ignoring", packetData.Length);
            return;
        }

        var header = GvspHeader.FromBytes(packetData);

        switch (header.PacketType)
        {
            case GvspPacketType.Leader:
                ProcessLeader(header, packetData);
                break;

            case GvspPacketType.Payload:
                ProcessPayload(header, packetData);
                break;

            case GvspPacketType.Trailer:
                ProcessTrailer(header, packetData);
                break;
        }
    }

    private void ProcessLeader(GvspHeader header, byte[] packetData)
    {
        var leaderPayload = packetData.AsSpan(GvspConstants.GenericHeaderSize);
        if (leaderPayload.Length < GvspConstants.ImageLeaderPayloadSize)
            return;

        var leader = GvspImageLeader.FromBytes(leaderPayload);

        _pendingFrames[header.BlockId] = new FrameAssembly
        {
            BlockId = header.BlockId,
            Leader = leader,
        };
    }

    private void ProcessPayload(GvspHeader header, byte[] packetData)
    {
        if (!_pendingFrames.TryGetValue(header.BlockId, out var assembly))
            return;

        var payloadData = packetData.AsSpan(GvspConstants.GenericHeaderSize).ToArray();
        assembly.PayloadChunks.Add((header.PacketId, payloadData));
    }

    private void ProcessTrailer(GvspHeader header, byte[] packetData)
    {
        if (!_pendingFrames.TryGetValue(header.BlockId, out var assembly))
            return;

        _pendingFrames.Remove(header.BlockId);

        // Reassemble payload data in packet order
        assembly.PayloadChunks.Sort((a, b) => a.packetId.CompareTo(b.packetId));

        var totalLength = 0;
        foreach (var (_, data) in assembly.PayloadChunks)
            totalLength += data.Length;

        var frameData = new byte[totalLength];
        var offset = 0;
        foreach (var (_, data) in assembly.PayloadChunks)
        {
            data.CopyTo(frameData, offset);
            offset += data.Length;
        }

        var frame = new GvspFrame
        {
            FrameId = assembly.BlockId,
            PayloadType = assembly.Leader.PayloadType,
            PixelFormat = assembly.Leader.PixelFormat,
            SizeX = assembly.Leader.SizeX,
            SizeY = assembly.Leader.SizeY,
            OffsetX = assembly.Leader.OffsetX,
            OffsetY = assembly.Leader.OffsetY,
            PaddingX = assembly.Leader.PaddingX,
            PaddingY = assembly.Leader.PaddingY,
            Timestamp = assembly.Leader.Timestamp,
            Data = frameData,
        };

        _logger.LogDebug("Frame {FrameId} assembled: {SizeX}x{SizeY}, {DataLength} bytes", frame.FrameId, frame.SizeX, frame.SizeY, frameData.Length);
        FrameReceived?.Invoke(this, frame);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transport.Dispose();
        GC.SuppressFinalize(this);
    }

    private class FrameAssembly
    {
        public ushort BlockId { get; init; }
        public GvspImageLeader Leader { get; set; }
        public List<(uint packetId, byte[] data)> PayloadChunks { get; } = [];
    }
}
