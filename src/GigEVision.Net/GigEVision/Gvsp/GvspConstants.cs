namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// GVSP packet type identifiers.
/// </summary>
public enum GvspPacketType : byte
{
    /// <summary>Leader packet — first packet of a block, contains image metadata.</summary>
    Leader = 1,

    /// <summary>Payload packet — contains image pixel data.</summary>
    Payload = 2,

    /// <summary>Trailer packet — last packet of a block, signals frame completion.</summary>
    Trailer = 3,
}

/// <summary>
/// GVSP payload type identifiers.
/// </summary>
public enum GvspPayloadType : ushort
{
    /// <summary>Payload contains image data.</summary>
    Image = 0x0001,

    /// <summary>Payload contains raw (unformatted) data.</summary>
    RawData = 0x0002,

    /// <summary>Payload contains chunk data (image + metadata).</summary>
    ChunkData = 0x0004,
}

/// <summary>
/// GVSP protocol constants.
/// </summary>
public static class GvspConstants
{
    /// <summary>Size of the GVSP generic header in bytes (Status(2) + BlockId(2) + PacketFormat(1) + PacketId(3)).</summary>
    public const int GenericHeaderSize = 8;

    /// <summary>Size of the GVSP image leader payload in bytes.</summary>
    public const int ImageLeaderPayloadSize = 36;
}
