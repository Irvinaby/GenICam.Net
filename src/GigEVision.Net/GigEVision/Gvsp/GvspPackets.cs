using System.Buffers.Binary;

namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// GVSP generic packet header (8 bytes, big-endian).
/// All GVSP packets (leader, payload, trailer) share this common header.
/// Format: [Status(2)] [BlockId(2)] [PacketFormat(1,upper nibble=PacketType)] [PacketId(3 bytes, 24-bit)]
/// </summary>
public readonly struct GvspHeader
{
    /// <summary>Status code (0 = success).</summary>
    public ushort Status { get; }

    /// <summary>Block ID identifying the frame this packet belongs to.</summary>
    public ushort BlockId { get; }

    /// <summary>Packet type (leader, payload, trailer).</summary>
    public GvspPacketType PacketType { get; }

    /// <summary>Packet ID within the block (24-bit, for ordering).</summary>
    public uint PacketId { get; }

    /// <summary>Creates a new GVSP header.</summary>
    public GvspHeader(ushort status, ushort blockId, GvspPacketType packetType, uint packetId)
    {
        Status = status;
        BlockId = blockId;
        PacketType = packetType;
        PacketId = packetId;
    }

    /// <summary>Serialises this header to 8 bytes.</summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[GvspConstants.GenericHeaderSize];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), Status);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), BlockId);
        // PacketFormat byte: upper 4 bits = packet type
        buffer[4] = (byte)((byte)PacketType << 4);
        // PacketId: 24-bit big-endian in bytes 5..7
        buffer[5] = (byte)((PacketId >> 16) & 0xFF);
        buffer[6] = (byte)((PacketId >> 8) & 0xFF);
        buffer[7] = (byte)(PacketId & 0xFF);
        return buffer;
    }

    /// <summary>Deserialises from 8 bytes.</summary>
    public static GvspHeader FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < GvspConstants.GenericHeaderSize)
            throw new ArgumentException($"Buffer must be at least {GvspConstants.GenericHeaderSize} bytes.", nameof(data));

        var status = BinaryPrimitives.ReadUInt16BigEndian(data[0..]);
        var blockId = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var packetType = (GvspPacketType)(data[4] >> 4);
        var packetId = (uint)((data[5] << 16) | (data[6] << 8) | data[7]);

        return new GvspHeader(status, blockId, packetType, packetId);
    }
}

/// <summary>
/// GVSP image leader payload, containing metadata about the incoming image frame.
/// </summary>
public readonly struct GvspImageLeader
{
    /// <summary>Payload type (Image, RawData, ChunkData).</summary>
    public GvspPayloadType PayloadType { get; }

    /// <summary>Timestamp from the camera (64-bit).</summary>
    public ulong Timestamp { get; }

    /// <summary>Pixel format as a 32-bit PFNC code.</summary>
    public uint PixelFormat { get; }

    /// <summary>Image width in pixels.</summary>
    public uint SizeX { get; }

    /// <summary>Image height in pixels.</summary>
    public uint SizeY { get; }

    /// <summary>Horizontal offset in pixels.</summary>
    public uint OffsetX { get; }

    /// <summary>Vertical offset in pixels.</summary>
    public uint OffsetY { get; }

    /// <summary>Horizontal padding bytes per line.</summary>
    public ushort PaddingX { get; }

    /// <summary>Vertical padding lines.</summary>
    public ushort PaddingY { get; }

    /// <summary>Creates a new image leader.</summary>
    public GvspImageLeader(GvspPayloadType payloadType, ulong timestamp, uint pixelFormat,
        uint sizeX, uint sizeY, uint offsetX, uint offsetY, ushort paddingX, ushort paddingY)
    {
        PayloadType = payloadType;
        Timestamp = timestamp;
        PixelFormat = pixelFormat;
        SizeX = sizeX;
        SizeY = sizeY;
        OffsetX = offsetX;
        OffsetY = offsetY;
        PaddingX = paddingX;
        PaddingY = paddingY;
    }

    /// <summary>Serialises this leader to bytes (following the generic header).</summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[GvspConstants.ImageLeaderPayloadSize];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), 0); // Reserved/field info
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)PayloadType);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(4), Timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(12), PixelFormat);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(16), SizeX);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(20), SizeY);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(24), OffsetX);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(28), OffsetY);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(32), PaddingX);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(34), PaddingY);
        return buffer;
    }

    /// <summary>Deserialises from bytes (after the generic header).</summary>
    public static GvspImageLeader FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < GvspConstants.ImageLeaderPayloadSize)
            throw new ArgumentException($"Leader payload must be at least {GvspConstants.ImageLeaderPayloadSize} bytes.", nameof(data));

        var payloadType = (GvspPayloadType)BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var timestamp = BinaryPrimitives.ReadUInt64BigEndian(data[4..]);
        var pixelFormat = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);
        var sizeX = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var sizeY = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        var offsetX = BinaryPrimitives.ReadUInt32BigEndian(data[24..]);
        var offsetY = BinaryPrimitives.ReadUInt32BigEndian(data[28..]);
        var paddingX = BinaryPrimitives.ReadUInt16BigEndian(data[32..]);
        var paddingY = BinaryPrimitives.ReadUInt16BigEndian(data[34..]);

        return new GvspImageLeader(payloadType, timestamp, pixelFormat, sizeX, sizeY, offsetX, offsetY, paddingX, paddingY);
    }
}

/// <summary>
/// GVSP image trailer payload, signalling completion of a frame block.
/// </summary>
public readonly struct GvspImageTrailer
{
    /// <summary>Payload type (should match the leader).</summary>
    public GvspPayloadType PayloadType { get; }

    /// <summary>Actual image height (may differ from leader SizeY for variable-height sensors).</summary>
    public uint SizeY { get; }

    /// <summary>Creates a new image trailer.</summary>
    public GvspImageTrailer(GvspPayloadType payloadType, uint sizeY)
    {
        PayloadType = payloadType;
        SizeY = sizeY;
    }

    /// <summary>Serialises to bytes.</summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), 0); // Reserved
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)PayloadType);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4), SizeY);
        return buffer;
    }

    /// <summary>Deserialises from bytes.</summary>
    public static GvspImageTrailer FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            throw new ArgumentException("Trailer payload must be at least 8 bytes.", nameof(data));

        var payloadType = (GvspPayloadType)BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var sizeY = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);

        return new GvspImageTrailer(payloadType, sizeY);
    }
}
