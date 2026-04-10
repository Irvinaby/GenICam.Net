using GenICam.Net.GigEVision.Gvsp;

namespace GenICam.Net.Tests.GigEVision.Gvsp;

[TestFixture]
public class GvspReceiverTests
{
    private static byte[] BuildLeaderPacket(ushort blockId, uint sizeX, uint sizeY, uint pixelFormat = 0x01080001)
    {
        var header = new GvspHeader(0, blockId, GvspPacketType.Leader, 0);
        var leader = new GvspImageLeader(GvspPayloadType.Image, 0, pixelFormat, sizeX, sizeY, 0, 0, 0, 0);

        var headerBytes = header.ToBytes();
        var leaderBytes = leader.ToBytes();

        var packet = new byte[headerBytes.Length + leaderBytes.Length];
        headerBytes.CopyTo(packet, 0);
        leaderBytes.CopyTo(packet, headerBytes.Length);
        return packet;
    }

    private static byte[] BuildPayloadPacket(ushort blockId, uint packetId, byte[] data)
    {
        var header = new GvspHeader(0, blockId, GvspPacketType.Payload, packetId);
        var headerBytes = header.ToBytes();

        var packet = new byte[headerBytes.Length + data.Length];
        headerBytes.CopyTo(packet, 0);
        data.CopyTo(packet, headerBytes.Length);
        return packet;
    }

    private static byte[] BuildTrailerPacket(ushort blockId, uint sizeY)
    {
        var header = new GvspHeader(0, blockId, GvspPacketType.Trailer, 0);
        var trailer = new GvspImageTrailer(GvspPayloadType.Image, sizeY);

        var headerBytes = header.ToBytes();
        var trailerBytes = trailer.ToBytes();

        var packet = new byte[headerBytes.Length + trailerBytes.Length];
        headerBytes.CopyTo(packet, 0);
        trailerBytes.CopyTo(packet, headerBytes.Length);
        return packet;
    }

    [Test]
    public void ProcessPacket_CompleteFrame_RaisesEvent()
    {
        var transport = new Tests.GigEVision.Gvcp.FakeUdpTransport();
        using var receiver = new GvspReceiver(transport);

        GvspFrame? receivedFrame = null;
        receiver.FrameReceived += (_, frame) => receivedFrame = frame;

        // Send leader
        receiver.ProcessPacket(BuildLeaderPacket(1, 4, 2));

        // Send 2 payload packets (4 bytes each = 4*2 = 8 bytes total for 4x2 image)
        receiver.ProcessPacket(BuildPayloadPacket(1, 1, new byte[] { 10, 20, 30, 40 }));
        receiver.ProcessPacket(BuildPayloadPacket(1, 2, new byte[] { 50, 60, 70, 80 }));

        // No frame yet
        Assert.That(receivedFrame, Is.Null);

        // Send trailer → triggers frame assembly
        receiver.ProcessPacket(BuildTrailerPacket(1, 2));

        Assert.That(receivedFrame, Is.Not.Null);
        Assert.That(receivedFrame!.FrameId, Is.EqualTo(1));
        Assert.That(receivedFrame.SizeX, Is.EqualTo(4));
        Assert.That(receivedFrame.SizeY, Is.EqualTo(2));
        Assert.That(receivedFrame.Data, Is.EqualTo(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 }));
    }

    [Test]
    public void ProcessPacket_OutOfOrderPayload_ReassemblesCorrectly()
    {
        var transport = new Tests.GigEVision.Gvcp.FakeUdpTransport();
        using var receiver = new GvspReceiver(transport);

        GvspFrame? receivedFrame = null;
        receiver.FrameReceived += (_, frame) => receivedFrame = frame;

        // Leader
        receiver.ProcessPacket(BuildLeaderPacket(1, 2, 1));

        // Send payloads out of order (packet 2 before packet 1)
        receiver.ProcessPacket(BuildPayloadPacket(1, 2, new byte[] { 0xBB }));
        receiver.ProcessPacket(BuildPayloadPacket(1, 1, new byte[] { 0xAA }));

        // Trailer
        receiver.ProcessPacket(BuildTrailerPacket(1, 1));

        Assert.That(receivedFrame, Is.Not.Null);
        // Should be sorted by packetId: 1 (0xAA) then 2 (0xBB)
        Assert.That(receivedFrame!.Data, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
    }

    [Test]
    public void ProcessPacket_TrailerWithoutLeader_NoEvent()
    {
        var transport = new Tests.GigEVision.Gvcp.FakeUdpTransport();
        using var receiver = new GvspReceiver(transport);

        GvspFrame? receivedFrame = null;
        receiver.FrameReceived += (_, frame) => receivedFrame = frame;

        // Trailer without leader → ignored
        receiver.ProcessPacket(BuildTrailerPacket(99, 100));

        Assert.That(receivedFrame, Is.Null);
    }

    [Test]
    public void ProcessPacket_TooShort_Ignored()
    {
        var transport = new Tests.GigEVision.Gvcp.FakeUdpTransport();
        using var receiver = new GvspReceiver(transport);

        GvspFrame? receivedFrame = null;
        receiver.FrameReceived += (_, frame) => receivedFrame = frame;

        // Too short to be a valid packet
        receiver.ProcessPacket(new byte[] { 0, 1, 2 });

        Assert.That(receivedFrame, Is.Null);
    }

    [Test]
    public void ProcessPacket_MultipleFrames_RaisesSeparateEvents()
    {
        var transport = new Tests.GigEVision.Gvcp.FakeUdpTransport();
        using var receiver = new GvspReceiver(transport);

        var frames = new List<GvspFrame>();
        receiver.FrameReceived += (_, frame) => frames.Add(frame);

        // Frame 1
        receiver.ProcessPacket(BuildLeaderPacket(1, 2, 1));
        receiver.ProcessPacket(BuildPayloadPacket(1, 1, new byte[] { 0x11 }));
        receiver.ProcessPacket(BuildTrailerPacket(1, 1));

        // Frame 2
        receiver.ProcessPacket(BuildLeaderPacket(2, 3, 1));
        receiver.ProcessPacket(BuildPayloadPacket(2, 1, new byte[] { 0x22, 0x33 }));
        receiver.ProcessPacket(BuildTrailerPacket(2, 1));

        Assert.That(frames, Has.Count.EqualTo(2));
        Assert.That(frames[0].FrameId, Is.EqualTo(1));
        Assert.That(frames[0].Data, Is.EqualTo(new byte[] { 0x11 }));
        Assert.That(frames[1].FrameId, Is.EqualTo(2));
        Assert.That(frames[1].Data, Is.EqualTo(new byte[] { 0x22, 0x33 }));
    }
}
