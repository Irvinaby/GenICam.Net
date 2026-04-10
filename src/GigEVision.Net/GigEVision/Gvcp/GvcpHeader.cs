using System.Buffers.Binary;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// GVCP command header (8 bytes, big-endian).
/// Format: [Key(1)] [Flags(1)] [Command(2)] [Length(2)] [ReqId(2)]
/// </summary>
public readonly struct GvcpCmdHeader
{
    /// <summary>Magic key byte (always 0x42).</summary>
    public byte Key { get; }

    /// <summary>Flag byte (bit 0 = Acknowledge required).</summary>
    public byte Flags { get; }

    /// <summary>Command type.</summary>
    public GvcpCommandType Command { get; }

    /// <summary>Payload length in bytes (excluding this header).</summary>
    public ushort Length { get; }

    /// <summary>Request ID for matching commands to acknowledgments.</summary>
    public ushort RequestId { get; }

    /// <summary>
    /// Creates a new GVCP command header.
    /// </summary>
    public GvcpCmdHeader(GvcpCommandType command, ushort length, ushort requestId, byte flags = 0x01)
    {
        Key = GvcpConstants.Key;
        Flags = flags;
        Command = command;
        Length = length;
        RequestId = requestId;
    }

    /// <summary>
    /// Serialises this header to an 8-byte big-endian buffer.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[GvcpConstants.CmdHeaderSize];
        buffer[0] = Key;
        buffer[1] = Flags;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)Command);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4), Length);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(6), RequestId);
        return buffer;
    }

    /// <summary>
    /// Deserialises a command header from an 8-byte big-endian buffer.
    /// </summary>
    public static GvcpCmdHeader FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < GvcpConstants.CmdHeaderSize)
            throw new ArgumentException($"Buffer must be at least {GvcpConstants.CmdHeaderSize} bytes.", nameof(data));

        var command = (GvcpCommandType)BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var length = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        var reqId = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);

        return new GvcpCmdHeader(command, length, reqId, data[1]);
    }
}

/// <summary>
/// GVCP acknowledgment header (8 bytes, big-endian).
/// Format: [Status(2)] [Acknowledge(2)] [Length(2)] [AckId(2)]
/// </summary>
public readonly struct GvcpAckHeader
{
    /// <summary>Status code indicating success or error.</summary>
    public GvcpStatus Status { get; }

    /// <summary>Acknowledgment command type (mirrors the original command + 1).</summary>
    public GvcpCommandType Acknowledge { get; }

    /// <summary>Payload length in bytes (excluding this header).</summary>
    public ushort Length { get; }

    /// <summary>Acknowledgment ID matching the original request ID.</summary>
    public ushort AckId { get; }

    /// <summary>
    /// Creates a new GVCP acknowledgment header.
    /// </summary>
    public GvcpAckHeader(GvcpStatus status, GvcpCommandType acknowledge, ushort length, ushort ackId)
    {
        Status = status;
        Acknowledge = acknowledge;
        Length = length;
        AckId = ackId;
    }

    /// <summary>
    /// Serialises this header to an 8-byte big-endian buffer.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[GvcpConstants.AckHeaderSize];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), (ushort)Status);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)Acknowledge);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4), Length);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(6), AckId);
        return buffer;
    }

    /// <summary>
    /// Deserialises an acknowledgment header from an 8-byte big-endian buffer.
    /// </summary>
    public static GvcpAckHeader FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < GvcpConstants.AckHeaderSize)
            throw new ArgumentException($"Buffer must be at least {GvcpConstants.AckHeaderSize} bytes.", nameof(data));

        var status = (GvcpStatus)BinaryPrimitives.ReadUInt16BigEndian(data[0..]);
        var ack = (GvcpCommandType)BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var length = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        var ackId = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);

        return new GvcpAckHeader(status, ack, length, ackId);
    }
}
