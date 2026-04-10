using GenICam.Net.GigEVision.Gvsp;

namespace GenICam.Net.Tests.GigEVision.Gvsp;

[TestFixture]
public class GvspFrameTests
{
    [Test]
    public void Frame_Properties_AreInitOnly()
    {
        var frame = new GvspFrame
        {
            FrameId = 1,
            PayloadType = GvspPayloadType.Image,
            PixelFormat = 0x01080001,
            SizeX = 1920,
            SizeY = 1080,
            OffsetX = 0,
            OffsetY = 0,
            PaddingX = 0,
            PaddingY = 0,
            Timestamp = 123456789UL,
            Data = new byte[1920 * 1080],
        };

        Assert.That(frame.FrameId, Is.EqualTo(1));
        Assert.That(frame.PayloadType, Is.EqualTo(GvspPayloadType.Image));
        Assert.That(frame.PixelFormat, Is.EqualTo(0x01080001));
        Assert.That(frame.SizeX, Is.EqualTo(1920));
        Assert.That(frame.SizeY, Is.EqualTo(1080));
        Assert.That(frame.Timestamp, Is.EqualTo(123456789UL));
        Assert.That(frame.Data.Length, Is.EqualTo(1920 * 1080));
    }

    [Test]
    public void Frame_DefaultData_IsEmpty()
    {
        var frame = new GvspFrame();
        Assert.That(frame.Data, Is.Empty);
    }
}
