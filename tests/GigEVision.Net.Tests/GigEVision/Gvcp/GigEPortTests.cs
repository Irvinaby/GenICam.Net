using System.Net;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GigEPortTests
{
    [Test]
    public void Read_SmallBlock_SingleRequest()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        var data = new byte[] { 1, 2, 3, 4 };
        var ack = GvcpPackets.BuildReadMemAck(1, GvcpStatus.Success, 0x1000, data);
        transport.EnqueueReceive(ack, ep);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        var result = port.Read(0x1000, 4);

        Assert.That(result, Is.EqualTo(data));
        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
    }

    [Test]
    public void Read_LargeBlock_ChunksRequests()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        // 600 bytes = 512 + 88 → 2 chunked requests
        var chunk1 = new byte[512];
        var chunk2 = new byte[88];
        Array.Fill<byte>(chunk1, 0xAA);
        Array.Fill<byte>(chunk2, 0xBB);

        transport.EnqueueReceive(GvcpPackets.BuildReadMemAck(1, GvcpStatus.Success, 0x1000, chunk1), ep);
        transport.EnqueueReceive(GvcpPackets.BuildReadMemAck(2, GvcpStatus.Success, 0x1200, chunk2), ep);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        var result = port.Read(0x1000, 600);

        Assert.That(result.Length, Is.EqualTo(600));
        Assert.That(result[0], Is.EqualTo(0xAA));
        Assert.That(result[512], Is.EqualTo(0xBB));
        Assert.That(transport.SentPackets, Has.Count.EqualTo(2));
    }

    [Test]
    public void Read_ZeroLength_ReturnsEmpty()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        var result = port.Read(0x1000, 0);

        Assert.That(result, Is.Empty);
        Assert.That(transport.SentPackets, Has.Count.EqualTo(0));
    }

    [Test]
    public void Write_SmallBlock_SingleRequest()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        transport.EnqueueReceive(GvcpPackets.BuildWriteMemAck(1), ep);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        port.Write(0x1000, new byte[] { 1, 2, 3, 4 });

        Assert.That(transport.SentPackets, Has.Count.EqualTo(1));
    }

    [Test]
    public void Write_LargeBlock_ChunksRequests()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        transport.EnqueueReceive(GvcpPackets.BuildWriteMemAck(1), ep);
        transport.EnqueueReceive(GvcpPackets.BuildWriteMemAck(2), ep);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        port.Write(0x1000, new byte[600]); // 512 + 88

        Assert.That(transport.SentPackets, Has.Count.EqualTo(2));
    }

    [Test]
    public void Write_EmptyData_NoRequest()
    {
        var transport = new FakeUdpTransport();
        var ep = new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port);

        using var client = new GvcpClient(transport, ep);
        var port = new GigEPort(client);

        port.Write(0x1000, []);

        Assert.That(transport.SentPackets, Has.Count.EqualTo(0));
    }
}
