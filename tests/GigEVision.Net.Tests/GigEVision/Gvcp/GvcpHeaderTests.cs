using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GvcpHeaderTests
{
    [Test]
    public void CmdHeader_RoundTrip_PreservesAllFields()
    {
        var header = new GvcpCmdHeader(GvcpCommandType.ReadRegCmd, 4, 42, 0x01);

        var bytes = header.ToBytes();
        var parsed = GvcpCmdHeader.FromBytes(bytes);

        Assert.That(parsed.Key, Is.EqualTo(GvcpConstants.Key));
        Assert.That(parsed.Flags, Is.EqualTo(0x01));
        Assert.That(parsed.Command, Is.EqualTo(GvcpCommandType.ReadRegCmd));
        Assert.That(parsed.Length, Is.EqualTo(4));
        Assert.That(parsed.RequestId, Is.EqualTo(42));
    }

    [Test]
    public void CmdHeader_ToBytes_IsExactly8Bytes()
    {
        var header = new GvcpCmdHeader(GvcpCommandType.DiscoveryCmd, 0, 1);
        var bytes = header.ToBytes();

        Assert.That(bytes.Length, Is.EqualTo(8));
        Assert.That(bytes[0], Is.EqualTo(0x42)); // magic key
    }

    [Test]
    public void CmdHeader_BigEndian_ByteOrder()
    {
        var header = new GvcpCmdHeader(GvcpCommandType.ReadRegCmd, 0x0004, 0x0001);
        var bytes = header.ToBytes();

        // Command = 0x0080 → bytes[2]=0x00, bytes[3]=0x80
        Assert.That(bytes[2], Is.EqualTo(0x00));
        Assert.That(bytes[3], Is.EqualTo(0x80));
        // Length = 0x0004 → bytes[4]=0x00, bytes[5]=0x04
        Assert.That(bytes[4], Is.EqualTo(0x00));
        Assert.That(bytes[5], Is.EqualTo(0x04));
    }

    [Test]
    public void AckHeader_RoundTrip_PreservesAllFields()
    {
        var header = new GvcpAckHeader(GvcpStatus.Success, GvcpCommandType.ReadRegAck, 4, 42);

        var bytes = header.ToBytes();
        var parsed = GvcpAckHeader.FromBytes(bytes);

        Assert.That(parsed.Status, Is.EqualTo(GvcpStatus.Success));
        Assert.That(parsed.Acknowledge, Is.EqualTo(GvcpCommandType.ReadRegAck));
        Assert.That(parsed.Length, Is.EqualTo(4));
        Assert.That(parsed.AckId, Is.EqualTo(42));
    }

    [Test]
    public void AckHeader_ErrorStatus_PreservesStatusCode()
    {
        var header = new GvcpAckHeader(GvcpStatus.AccessDenied, GvcpCommandType.WriteRegAck, 0, 10);

        var bytes = header.ToBytes();
        var parsed = GvcpAckHeader.FromBytes(bytes);

        Assert.That(parsed.Status, Is.EqualTo(GvcpStatus.AccessDenied));
    }

    [Test]
    public void CmdHeader_FromBytes_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => GvcpCmdHeader.FromBytes(new byte[4]));
    }

    [Test]
    public void AckHeader_FromBytes_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => GvcpAckHeader.FromBytes(new byte[4]));
    }
}
