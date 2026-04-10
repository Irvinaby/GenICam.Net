using System.Buffers.Binary;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Serialisation helpers for GVCP command and acknowledgment payloads.
/// All multi-byte fields use big-endian (network byte order).
/// </summary>
public static class GvcpPackets
{
    // ── ReadReg ──────────────────────────────────────────────

    /// <summary>
    /// Builds a READREG_CMD packet to read one or more 32-bit register addresses.
    /// </summary>
    public static byte[] BuildReadRegCmd(ushort requestId, params uint[] addresses)
    {
        var payloadLen = (ushort)(addresses.Length * 4);
        var header = new GvcpCmdHeader(GvcpCommandType.ReadRegCmd, payloadLen, requestId);

        var packet = new byte[GvcpConstants.CmdHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        for (var i = 0; i < addresses.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(GvcpConstants.CmdHeaderSize + i * 4), addresses[i]);
        }

        return packet;
    }

    /// <summary>
    /// Parses the register values from a READREG_ACK payload.
    /// </summary>
    public static uint[] ParseReadRegAck(ReadOnlySpan<byte> packet)
    {
        var ackHeader = GvcpAckHeader.FromBytes(packet);
        var count = ackHeader.Length / 4;
        var values = new uint[count];

        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32BigEndian(packet[(GvcpConstants.AckHeaderSize + i * 4)..]);
        }

        return values;
    }

    // ── WriteReg ─────────────────────────────────────────────

    /// <summary>
    /// Builds a WRITEREG_CMD packet to write one or more address-value pairs.
    /// </summary>
    public static byte[] BuildWriteRegCmd(ushort requestId, params (uint address, uint value)[] entries)
    {
        var payloadLen = (ushort)(entries.Length * 8);
        var header = new GvcpCmdHeader(GvcpCommandType.WriteRegCmd, payloadLen, requestId);

        var packet = new byte[GvcpConstants.CmdHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = GvcpConstants.CmdHeaderSize + i * 8;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), entries[i].address);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset + 4), entries[i].value);
        }

        return packet;
    }

    /// <summary>
    /// Builds a WRITEREG_ACK response packet.
    /// </summary>
    public static byte[] BuildWriteRegAck(ushort ackId, GvcpStatus status = GvcpStatus.Success)
    {
        var header = new GvcpAckHeader(status, GvcpCommandType.WriteRegAck, 0, ackId);
        return header.ToBytes();
    }

    // ── ReadMem ──────────────────────────────────────────────

    /// <summary>
    /// Builds a READMEM_CMD packet to read a block of memory.
    /// Payload: Address(4) + Reserved(2) + Count(2) = 8 bytes.
    /// </summary>
    public static byte[] BuildReadMemCmd(ushort requestId, uint address, ushort count)
    {
        const ushort payloadLen = 8;
        var header = new GvcpCmdHeader(GvcpCommandType.ReadMemCmd, payloadLen, requestId);

        var packet = new byte[GvcpConstants.CmdHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(GvcpConstants.CmdHeaderSize), address);
        // Reserved 2 bytes (zeros)
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(GvcpConstants.CmdHeaderSize + 6), count);

        return packet;
    }

    /// <summary>
    /// Parses the address and data from a READMEM_ACK payload.
    /// ACK payload: Address(4) + Data(N).
    /// </summary>
    public static (uint address, byte[] data) ParseReadMemAck(ReadOnlySpan<byte> packet)
    {
        var ackHeader = GvcpAckHeader.FromBytes(packet);
        var address = BinaryPrimitives.ReadUInt32BigEndian(packet[GvcpConstants.AckHeaderSize..]);
        var dataLen = ackHeader.Length - 4;
        var data = packet.Slice(GvcpConstants.AckHeaderSize + 4, dataLen).ToArray();

        return (address, data);
    }

    // ── WriteMem ─────────────────────────────────────────────

    /// <summary>
    /// Builds a WRITEMEM_CMD packet to write a block of memory.
    /// Payload: Address(4) + Data(N).
    /// </summary>
    public static byte[] BuildWriteMemCmd(ushort requestId, uint address, byte[] data)
    {
        var payloadLen = (ushort)(4 + data.Length);
        var header = new GvcpCmdHeader(GvcpCommandType.WriteMemCmd, payloadLen, requestId);

        var packet = new byte[GvcpConstants.CmdHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(GvcpConstants.CmdHeaderSize), address);
        data.CopyTo(packet, GvcpConstants.CmdHeaderSize + 4);

        return packet;
    }

    /// <summary>
    /// Builds a WRITEMEM_ACK response packet.
    /// ACK payload: Reserved(2) + Index(2) = 4 bytes.
    /// </summary>
    public static byte[] BuildWriteMemAck(ushort ackId, ushort index = 0, GvcpStatus status = GvcpStatus.Success)
    {
        var header = new GvcpAckHeader(status, GvcpCommandType.WriteMemAck, 4, ackId);
        var packet = new byte[GvcpConstants.AckHeaderSize + 4];
        header.ToBytes().CopyTo(packet, 0);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(GvcpConstants.AckHeaderSize + 2), index);
        return packet;
    }

    // ── Discovery ────────────────────────────────────────────

    /// <summary>
    /// Builds a DISCOVERY_CMD packet (no payload beyond the header).
    /// Flag 0x11: allow broadcast + require ACK.
    /// </summary>
    public static byte[] BuildDiscoveryCmd(ushort requestId)
    {
        var header = new GvcpCmdHeader(GvcpCommandType.DiscoveryCmd, 0, requestId, flags: 0x11);
        return header.ToBytes();
    }

    /// <summary>
    /// Parses a DISCOVERY_ACK payload into a <see cref="GigECameraInfo"/>.
    /// The discovery ACK payload is a fixed 248-byte block containing device information.
    /// </summary>
    public static GigECameraInfo ParseDiscoveryAck(ReadOnlySpan<byte> packet)
    {
        var ackHeader = GvcpAckHeader.FromBytes(packet);
        var payload = packet[GvcpConstants.AckHeaderSize..];

        return GigECameraInfo.FromPayload(payload);
    }

    // ── ForceIP ──────────────────────────────────────────────

    /// <summary>
    /// Builds a FORCEIP_CMD packet.
    /// Payload: MAC(6) + Reserved(2) + StaticIP(4) + Reserved(12) + SubnetMask(4) + Reserved(12) + Gateway(4) + Reserved(12) = 56 bytes.
    /// </summary>
    public static byte[] BuildForceIpCmd(ushort requestId, byte[] macAddress, uint ipAddress, uint subnetMask, uint gateway)
    {
        const ushort payloadLen = 56;
        var header = new GvcpCmdHeader(GvcpCommandType.ForceIpCmd, payloadLen, requestId);

        var packet = new byte[GvcpConstants.CmdHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        var offset = GvcpConstants.CmdHeaderSize;

        // MAC address at offset+2 (first 2 bytes reserved)
        macAddress.AsSpan(0, Math.Min(6, macAddress.Length)).CopyTo(packet.AsSpan(offset + 2));
        offset += 8; // 2 reserved + 6 MAC

        // Reserved(12) then Static IP(4)
        offset += 12;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), ipAddress);
        offset += 4;

        // Reserved(12) then Subnet Mask(4)
        offset += 12;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), subnetMask);
        offset += 4;

        // Reserved(12) then Gateway(4)
        offset += 12;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), gateway);

        return packet;
    }

    /// <summary>
    /// Builds a FORCEIP_ACK response packet (no payload).
    /// </summary>
    public static byte[] BuildForceIpAck(ushort ackId, GvcpStatus status = GvcpStatus.Success)
    {
        var header = new GvcpAckHeader(status, GvcpCommandType.ForceIpAck, 0, ackId);
        return header.ToBytes();
    }

    /// <summary>
    /// Builds a READREG_ACK response packet with the given register values.
    /// </summary>
    public static byte[] BuildReadRegAck(ushort ackId, GvcpStatus status, params uint[] values)
    {
        var payloadLen = (ushort)(values.Length * 4);
        var header = new GvcpAckHeader(status, GvcpCommandType.ReadRegAck, payloadLen, ackId);

        var packet = new byte[GvcpConstants.AckHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(GvcpConstants.AckHeaderSize + i * 4), values[i]);
        }

        return packet;
    }

    /// <summary>
    /// Builds a READMEM_ACK response packet with the given address and data.
    /// </summary>
    public static byte[] BuildReadMemAck(ushort ackId, GvcpStatus status, uint address, byte[] data)
    {
        var payloadLen = (ushort)(4 + data.Length);
        var header = new GvcpAckHeader(status, GvcpCommandType.ReadMemAck, payloadLen, ackId);

        var packet = new byte[GvcpConstants.AckHeaderSize + payloadLen];
        header.ToBytes().CopyTo(packet, 0);

        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(GvcpConstants.AckHeaderSize), address);
        data.CopyTo(packet, GvcpConstants.AckHeaderSize + 4);

        return packet;
    }
}
