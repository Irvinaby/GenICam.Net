using System.Buffers.Binary;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GvcpPacketsTests
{
    [Test]
    public void ReadRegCmd_RoundTrip()
    {
        var packet = GvcpPackets.BuildReadRegCmd(1, 0x00001000);

        var header = GvcpCmdHeader.FromBytes(packet);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.ReadRegCmd));
        Assert.That(header.Length, Is.EqualTo(4));
        Assert.That(header.RequestId, Is.EqualTo(1));

        var address = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(8));
        Assert.That(address, Is.EqualTo(0x00001000));
    }

    [Test]
    public void ReadRegAck_ParsesValues()
    {
        var ack = GvcpPackets.BuildReadRegAck(1, GvcpStatus.Success, 0xDEADBEEF);
        var values = GvcpPackets.ParseReadRegAck(ack);

        Assert.That(values, Has.Length.EqualTo(1));
        Assert.That(values[0], Is.EqualTo(0xDEADBEEF));
    }

    [Test]
    public void ReadRegCmd_MultipleAddresses()
    {
        var packet = GvcpPackets.BuildReadRegCmd(2, 0x1000, 0x2000, 0x3000);
        var header = GvcpCmdHeader.FromBytes(packet);

        Assert.That(header.Length, Is.EqualTo(12)); // 3 * 4 bytes
    }

    [Test]
    public void WriteRegCmd_RoundTrip()
    {
        var packet = GvcpPackets.BuildWriteRegCmd(3, (0x1000, 0xAABBCCDD));

        var header = GvcpCmdHeader.FromBytes(packet);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.WriteRegCmd));
        Assert.That(header.Length, Is.EqualTo(8)); // 1 entry * 8 bytes

        var addr = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(8));
        var val = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(12));
        Assert.That(addr, Is.EqualTo(0x1000));
        Assert.That(val, Is.EqualTo(0xAABBCCDD));
    }

    [Test]
    public void WriteRegAck_Success()
    {
        var ack = GvcpPackets.BuildWriteRegAck(3);
        var header = GvcpAckHeader.FromBytes(ack);

        Assert.That(header.Status, Is.EqualTo(GvcpStatus.Success));
        Assert.That(header.Acknowledge, Is.EqualTo(GvcpCommandType.WriteRegAck));
        Assert.That(header.AckId, Is.EqualTo(3));
    }

    [Test]
    public void ReadMemCmd_RoundTrip()
    {
        var packet = GvcpPackets.BuildReadMemCmd(4, 0x2000, 256);

        var header = GvcpCmdHeader.FromBytes(packet);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.ReadMemCmd));
        Assert.That(header.Length, Is.EqualTo(8));

        var addr = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(8));
        Assert.That(addr, Is.EqualTo(0x2000));
    }

    [Test]
    public void ReadMemAck_RoundTrip()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var ack = GvcpPackets.BuildReadMemAck(4, GvcpStatus.Success, 0x2000, data);

        var (address, parsedData) = GvcpPackets.ParseReadMemAck(ack);
        Assert.That(address, Is.EqualTo(0x2000));
        Assert.That(parsedData, Is.EqualTo(data));
    }

    [Test]
    public void WriteMemCmd_RoundTrip()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC };
        var packet = GvcpPackets.BuildWriteMemCmd(5, 0x3000, data);

        var header = GvcpCmdHeader.FromBytes(packet);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.WriteMemCmd));
        Assert.That(header.Length, Is.EqualTo(7)); // 4 addr + 3 data

        var addr = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(8));
        Assert.That(addr, Is.EqualTo(0x3000));

        var parsedData = packet.AsSpan(12, 3).ToArray();
        Assert.That(parsedData, Is.EqualTo(data));
    }

    [Test]
    public void DiscoveryCmd_HasCorrectFlags()
    {
        var packet = GvcpPackets.BuildDiscoveryCmd(1);
        var header = GvcpCmdHeader.FromBytes(packet);

        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.DiscoveryCmd));
        Assert.That(header.Flags, Is.EqualTo(0x11)); // broadcast + ack required
        Assert.That(header.Length, Is.EqualTo(0));
    }

    [Test]
    public void ForceIpCmd_ContainsMacAndAddresses()
    {
        var mac = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
        var packet = GvcpPackets.BuildForceIpCmd(6, mac, 0xC0A80064, 0xFFFFFF00, 0xC0A80001);

        var header = GvcpCmdHeader.FromBytes(packet);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.ForceIpCmd));
        Assert.That(header.Length, Is.EqualTo(56));
        Assert.That(packet.Length, Is.EqualTo(64)); // 8 header + 56 payload
    }

    [Test]
    public void ForceIpAck_Success()
    {
        var ack = GvcpPackets.BuildForceIpAck(6);
        var header = GvcpAckHeader.FromBytes(ack);

        Assert.That(header.Status, Is.EqualTo(GvcpStatus.Success));
        Assert.That(header.Acknowledge, Is.EqualTo(GvcpCommandType.ForceIpAck));
    }
}
