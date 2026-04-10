using GenICam.Net.GigEVision.Gvsp;

namespace GenICam.Net.Tests.GigEVision.Gvsp;

[TestFixture]
public class GvspPacketsTests
{
    [Test]
    public void GvspHeader_RoundTrip()
    {
        var header = new GvspHeader(0, 42, GvspPacketType.Leader, 0);

        var bytes = header.ToBytes();
        var parsed = GvspHeader.FromBytes(bytes);

        Assert.That(parsed.Status, Is.EqualTo(0));
        Assert.That(parsed.BlockId, Is.EqualTo(42));
        Assert.That(parsed.PacketType, Is.EqualTo(GvspPacketType.Leader));
        Assert.That(parsed.PacketId, Is.EqualTo(0u));
    }

    [Test]
    public void GvspHeader_PayloadType_RoundTrip()
    {
        var header = new GvspHeader(0, 1, GvspPacketType.Payload, 5);

        var bytes = header.ToBytes();
        var parsed = GvspHeader.FromBytes(bytes);

        Assert.That(parsed.PacketType, Is.EqualTo(GvspPacketType.Payload));
        Assert.That(parsed.PacketId, Is.EqualTo(5u));
    }

    [Test]
    public void GvspHeader_TrailerType()
    {
        var header = new GvspHeader(0, 1, GvspPacketType.Trailer, 10);

        var bytes = header.ToBytes();
        var parsed = GvspHeader.FromBytes(bytes);

        Assert.That(parsed.PacketType, Is.EqualTo(GvspPacketType.Trailer));
        Assert.That(parsed.PacketId, Is.EqualTo(10u));
    }

    [Test]
    public void GvspHeader_LargePacketId()
    {
        // 24-bit max = 0xFFFFFF
        var header = new GvspHeader(0, 1, GvspPacketType.Payload, 0x00FFFFFF);

        var bytes = header.ToBytes();
        var parsed = GvspHeader.FromBytes(bytes);

        Assert.That(parsed.PacketId, Is.EqualTo(0x00FFFFFFu));
    }

    [Test]
    public void GvspHeader_FromBytes_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => GvspHeader.FromBytes(new byte[4]));
    }

    [Test]
    public void ImageLeader_RoundTrip()
    {
        var leader = new GvspImageLeader(
            GvspPayloadType.Image, 123456789UL, 0x01080001,
            1920, 1080, 0, 0, 0, 0);

        var bytes = leader.ToBytes();
        var parsed = GvspImageLeader.FromBytes(bytes);

        Assert.That(parsed.PayloadType, Is.EqualTo(GvspPayloadType.Image));
        Assert.That(parsed.Timestamp, Is.EqualTo(123456789UL));
        Assert.That(parsed.PixelFormat, Is.EqualTo(0x01080001));
        Assert.That(parsed.SizeX, Is.EqualTo(1920));
        Assert.That(parsed.SizeY, Is.EqualTo(1080));
        Assert.That(parsed.OffsetX, Is.EqualTo(0));
        Assert.That(parsed.OffsetY, Is.EqualTo(0));
        Assert.That(parsed.PaddingX, Is.EqualTo(0));
        Assert.That(parsed.PaddingY, Is.EqualTo(0));
    }

    [Test]
    public void ImageLeader_WithOffsets()
    {
        var leader = new GvspImageLeader(
            GvspPayloadType.Image, 0, 0,
            640, 480, 100, 50, 4, 2);

        var bytes = leader.ToBytes();
        var parsed = GvspImageLeader.FromBytes(bytes);

        Assert.That(parsed.OffsetX, Is.EqualTo(100));
        Assert.That(parsed.OffsetY, Is.EqualTo(50));
        Assert.That(parsed.PaddingX, Is.EqualTo(4));
        Assert.That(parsed.PaddingY, Is.EqualTo(2));
    }

    [Test]
    public void ImageLeader_FromBytes_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => GvspImageLeader.FromBytes(new byte[10]));
    }

    [Test]
    public void ImageTrailer_RoundTrip()
    {
        var trailer = new GvspImageTrailer(GvspPayloadType.Image, 1080);

        var bytes = trailer.ToBytes();
        var parsed = GvspImageTrailer.FromBytes(bytes);

        Assert.That(parsed.PayloadType, Is.EqualTo(GvspPayloadType.Image));
        Assert.That(parsed.SizeY, Is.EqualTo(1080));
    }

    [Test]
    public void ImageTrailer_FromBytes_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => GvspImageTrailer.FromBytes(new byte[4]));
    }
}
