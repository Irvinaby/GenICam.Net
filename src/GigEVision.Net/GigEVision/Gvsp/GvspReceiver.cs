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
    private const int MaxPendingFrames = 4;

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
        TrimPendingFrames();
    }

    private void ProcessPayload(GvspHeader header, byte[] packetData)
    {
        if (!_pendingFrames.TryGetValue(header.BlockId, out var assembly))
            return;

        if (assembly.PayloadChunks.ContainsKey(header.PacketId))
            return;

        var payloadData = packetData.AsSpan(GvspConstants.GenericHeaderSize).ToArray();
        assembly.PayloadChunks.Add(header.PacketId, payloadData);
    }

    private void ProcessTrailer(GvspHeader header, byte[] packetData)
    {
        if (!_pendingFrames.TryGetValue(header.BlockId, out var assembly))
            return;

        if (packetData.Length < GvspConstants.GenericHeaderSize + GvspConstants.ImageTrailerPayloadSize)
            return;

        _pendingFrames.Remove(header.BlockId);
        TryCompleteFrame(assembly);
    }

    private void TrimPendingFrames()
    {
        if (_pendingFrames.Count <= MaxPendingFrames)
            return;

        var oldest = _pendingFrames.Values.MinBy(assembly => assembly.Sequence);
        if (oldest is null)
            return;

        if (_pendingFrames.Remove(oldest.BlockId))
        {
            _logger.LogDebug(
                "Dropping incomplete frame {FrameId}: pending frame limit exceeded",
                oldest.BlockId);
        }
    }

    private void TryCompleteFrame(FrameAssembly assembly)
    {
        var totalLength = 0;
        foreach (var data in assembly.PayloadChunks.Values)
            totalLength += data.Length;

        var frameData = new byte[totalLength];
        var offset = 0;
        foreach (var data in assembly.PayloadChunks.OrderBy(chunk => chunk.Key).Select(chunk => chunk.Value))
        {
            data.CopyTo(frameData, offset);
            offset += data.Length;
        }

        var expectedLength = GetExpectedPayloadLength(assembly.Leader);
        if (expectedLength is not null && frameData.Length < expectedLength.Value)
        {
            _logger.LogWarning("Dropping incomplete frame {FrameId}: payload {Actual} < {Expected} bytes",
                assembly.BlockId, frameData.Length, expectedLength.Value);
            return;
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

    private static long? GetExpectedPayloadLength(GvspImageLeader leader)
    {
        if (leader.PayloadType != GvspPayloadType.Image)
            return null;

        var pixelCount = checked((long)leader.SizeX * leader.SizeY);
        var imageBytes = leader.PixelFormat switch
        {
            0x01080001 or 0x01080008 or 0x01080009 or 0x0108000A or 0x0108000B => pixelCount,
            0x01100003 or 0x01100005 or 0x01100007 => pixelCount * 2,
            0x010C0004 => (pixelCount * 10 + 7) / 8,
            0x010C0006 => (pixelCount * 12 + 7) / 8,
            0x02180014 or 0x02180015 => pixelCount * 3,
            0x02200016 or 0x02200017 => pixelCount * 4,
            _ => (long?)null,
        };

        if (imageBytes is null)
            return null;

        return imageBytes.Value + ((long)leader.PaddingX * leader.SizeY) + ((long)leader.PaddingY * leader.SizeX);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transport.Dispose();
        GC.SuppressFinalize(this);
    }

    private class FrameAssembly
    {
        private static long _nextSequence;

        public ushort BlockId { get; init; }
        public long Sequence { get; } = Interlocked.Increment(ref _nextSequence);
        public GvspImageLeader Leader { get; set; }
        public Dictionary<uint, byte[]> PayloadChunks { get; } = [];
    }
}
