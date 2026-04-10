using System.Net;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GvcpClientTests
{
    [Test]
    public async Task ReadRegisterAsync_ReturnsValue()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        // Enqueue a successful ACK with value 0x12345678
        var ack = GvcpPackets.BuildReadRegAck(1, GvcpStatus.Success, 0x12345678);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);
        var value = await client.ReadRegisterAsync(0x1000);

        Assert.That(value, Is.EqualTo(0x12345678));
        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task WriteRegisterAsync_SendsCorrectPacket()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        var ack = GvcpPackets.BuildWriteRegAck(1);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);
        await client.WriteRegisterAsync(0x1000, 0xAABBCCDD);

        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
        var sent = transport.SentPackets[0].Data;
        var header = GvcpCmdHeader.FromBytes(sent);
        Assert.That(header.Command, Is.EqualTo(GvcpCommandType.WriteRegCmd));
    }

    [Test]
    public async Task ReadMemoryAsync_ReturnsData()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var ack = GvcpPackets.BuildReadMemAck(1, GvcpStatus.Success, 0x2000, data);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);
        var result = await client.ReadMemoryAsync(0x2000, 4);

        Assert.That(result, Is.EqualTo(data));
    }

    [Test]
    public async Task WriteMemoryAsync_SendsCorrectPacket()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        var ack = GvcpPackets.BuildWriteMemAck(1);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);
        await client.WriteMemoryAsync(0x3000, new byte[] { 0xAA, 0xBB });

        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReadRegisterAsync_ErrorStatus_ThrowsGvcpException()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        var ack = GvcpPackets.BuildReadRegAck(1, GvcpStatus.InvalidAddress);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);

        var ex = Assert.ThrowsAsync<GvcpException>(async () =>
            await client.ReadRegisterAsync(0xFFFFFFFF));

        Assert.That(ex!.Status, Is.EqualTo(GvcpStatus.InvalidAddress));
    }

    [Test]
    public void ReadRegisterAsync_Timeout_ThrowsTimeoutException()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        // Don't enqueue any response → simulates timeout
        using var client = new GvcpClient(transport, ep, timeoutMs: 100);

        Assert.ThrowsAsync<TimeoutException>(async () =>
            await client.ReadRegisterAsync(0x1000));
    }

    [Test]
    public void WriteRegisterAsync_ErrorStatus_ThrowsGvcpException()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Parse("192.168.1.100"), GvcpConstants.Port);

        var ack = GvcpPackets.BuildWriteRegAck(1, GvcpStatus.WriteProtect);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);

        var ex = Assert.ThrowsAsync<GvcpException>(async () =>
            await client.WriteRegisterAsync(0x1000, 0x00));

        Assert.That(ex!.Status, Is.EqualTo(GvcpStatus.WriteProtect));
    }
}
