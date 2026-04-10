namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Represents a complete image frame reassembled from GVSP packets.
/// Immutable after construction.
/// </summary>
public class GvspFrame
{
    /// <summary>Block/frame ID from the GVSP header.</summary>
    public ushort FrameId { get; init; }

    /// <summary>Payload type (Image, RawData, ChunkData).</summary>
    public GvspPayloadType PayloadType { get; init; }

    /// <summary>Pixel format as a 32-bit PFNC code.</summary>
    public uint PixelFormat { get; init; }

    /// <summary>Image width in pixels.</summary>
    public uint SizeX { get; init; }

    /// <summary>Image height in pixels.</summary>
    public uint SizeY { get; init; }

    /// <summary>Horizontal offset in pixels.</summary>
    public uint OffsetX { get; init; }

    /// <summary>Vertical offset in pixels.</summary>
    public uint OffsetY { get; init; }

    /// <summary>Horizontal padding bytes per line.</summary>
    public ushort PaddingX { get; init; }

    /// <summary>Vertical padding lines.</summary>
    public ushort PaddingY { get; init; }

    /// <summary>Camera timestamp from the leader packet.</summary>
    public ulong Timestamp { get; init; }

    /// <summary>Raw pixel data bytes, reassembled in packet order.</summary>
    public byte[] Data { get; init; } = [];
}
