using System.Net;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GigEDiscoveryTests
{
    [Test]
    public async Task DiscoverAsync_CollectsMultipleCameras()
    {
        var transport = new FakeUdpTransport();

        var cam1 = new GigECameraInfo
        {
            SpecVersionMajor = 2,
            SpecVersionMinor = 0,
            MacAddress = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 },
            ManufacturerName = "VendorA",
            ModelName = "CamX",
            SerialNumber = "SN001",
            IpAddress = new IPAddress(new byte[] { 192, 168, 1, 100 }),
            SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 }),
            Gateway = new IPAddress(new byte[] { 192, 168, 1, 1 }),
        };

        var cam2 = new GigECameraInfo
        {
            SpecVersionMajor = 2,
            SpecVersionMinor = 0,
            MacAddress = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x66 },
            ManufacturerName = "VendorB",
            ModelName = "CamY",
            SerialNumber = "SN002",
            IpAddress = new IPAddress(new byte[] { 192, 168, 1, 101 }),
            SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 }),
            Gateway = new IPAddress(new byte[] { 192, 168, 1, 1 }),
        };

        // Build discovery ACK packets
        var ackPayload1 = cam1.ToPayload();
        var ackHeader1 = new GvcpAckHeader(GvcpStatus.Success, GvcpCommandType.DiscoveryAck, (ushort)ackPayload1.Length, 1);
        var ack1 = new byte[GvcpConstants.AckHeaderSize + ackPayload1.Length];
        ackHeader1.ToBytes().CopyTo(ack1, 0);
        ackPayload1.CopyTo(ack1, GvcpConstants.AckHeaderSize);

        var ackPayload2 = cam2.ToPayload();
        var ackHeader2 = new GvcpAckHeader(GvcpStatus.Success, GvcpCommandType.DiscoveryAck, (ushort)ackPayload2.Length, 1);
        var ack2 = new byte[GvcpConstants.AckHeaderSize + ackPayload2.Length];
        ackHeader2.ToBytes().CopyTo(ack2, 0);
        ackPayload2.CopyTo(ack2, GvcpConstants.AckHeaderSize);

        transport.EnqueueReceive(ack1, new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port));
        transport.EnqueueReceive(ack2, new IPEndPoint(IPAddress.Parse("192.168.1.101"), GvcpConstants.Port));

        using var discovery = new GigEDiscovery(transport);
        var cameras = await discovery.DiscoverAsync(timeoutMs: 500);

        Assert.That(cameras, Has.Count.EqualTo(2));
        Assert.That(cameras[0].ManufacturerName, Is.EqualTo("VendorA"));
        Assert.That(cameras[0].ModelName, Is.EqualTo("CamX"));
        Assert.That(cameras[1].ManufacturerName, Is.EqualTo("VendorB"));
        Assert.That(cameras[1].SerialNumber, Is.EqualTo("SN002"));
    }

    [Test]
    public async Task DiscoverAsync_NoCameras_ReturnsEmpty()
    {
        var transport = new FakeUdpTransport();

        using var discovery = new GigEDiscovery(transport);
        var cameras = await discovery.DiscoverAsync(timeoutMs: 100);

        Assert.That(cameras, Is.Empty);
    }

    [Test]
    public async Task DiscoverAsync_EnablesBroadcast()
    {
        var transport = new FakeUdpTransport();

        using var discovery = new GigEDiscovery(transport);
        await discovery.DiscoverAsync(timeoutMs: 100);

        Assert.That(transport.BroadcastEnabled, Is.True);
    }

    [Test]
    public async Task DiscoverAsync_SendsDiscoveryCmd()
    {
        var transport = new FakeUdpTransport();

        using var discovery = new GigEDiscovery(transport);
        await discovery.DiscoverAsync(timeoutMs: 100);

        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
        var sent = transport.SentPackets[0].Data;
        var header = GvcpCmdHeader.FromBytes(sent);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.DiscoveryCmd));
    }
}
