using System.Net;
using System.Net.Sockets;
using System.Text;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;

namespace GenICam.Net.Tests.GigEVision.Gvcp;

[TestFixture]
public class GvcpXmlLoaderTests
{
    [Test]
    public async Task LoadNodeMapAsync_FirstLocalXmlAccessDenied_FallsBackToSecondUrl()
    {
        var transport = new FakeUdpTransport();
        using var client = new GvcpClient(transport, new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port));

        const uint inaccessibleXmlAddress = 0x00002000;
        const uint fallbackXmlAddress = 0x00003000;
        var xml = Encoding.UTF8.GetBytes("""
            <RegisterDescription>
              <Integer Name="Width">
                <Value>640</Value>
              </Integer>
            </RegisterDescription>
            """);

        transport.EnqueueReceive(BuildReadMemAck(1, GvcpConstants.FirstUrlRegister,
            BuildBootstrapUrl($"Local:blocked.xml;0x{inaccessibleXmlAddress:X8};0x00000004")));
        transport.EnqueueReceive(BuildReadMemAck(2, GvcpConstants.SecondUrlRegister,
            BuildBootstrapUrl($"Local:fallback.xml;0x{fallbackXmlAddress:X8};0x{xml.Length:X8}")));
        transport.EnqueueReceive(new GvcpAckHeader(GvcpStatus.AccessDenied, GvcpCommandType.ReadMemAck, 0, 3).ToBytes());
        transport.EnqueueReceive(BuildReadMemAck(4, fallbackXmlAddress, PadToReadMemoryAlignment(xml)));

        var nodeMap = await GvcpXmlLoader.LoadNodeMapAsync(client);

        Assert.That(nodeMap.GetNode("Width"), Is.TypeOf<IntegerNode>());
        Assert.That(((IInteger)nodeMap.GetNode("Width")!).Value, Is.EqualTo(640));
    }

    [Test]
    public async Task LoadNodeMapAsync_HttpXmlUrl_SendsBrowserUserAgent()
    {
        var xml = """
            <RegisterDescription>
              <Integer Name="Height">
                <Value>480</Value>
              </Integer>
            </RegisterDescription>
            """;
        using var server = await BrowserUserAgentXmlServer.StartAsync(xml);

        var transport = new FakeUdpTransport();
        using var client = new GvcpClient(transport, new IPEndPoint(IPAddress.Loopback, GvcpConstants.Port));
        transport.EnqueueReceive(BuildReadMemAck(1, GvcpConstants.FirstUrlRegister, BuildBootstrapUrl(server.Url)));
        transport.EnqueueReceive(BuildReadMemAck(2, GvcpConstants.SecondUrlRegister, BuildBootstrapUrl(string.Empty)));

        var nodeMap = await GvcpXmlLoader.LoadNodeMapAsync(client);

        Assert.That(nodeMap.GetNode("Height"), Is.TypeOf<IntegerNode>());
        Assert.That(((IInteger)nodeMap.GetNode("Height")!).Value, Is.EqualTo(480));
    }

    private static byte[] BuildBootstrapUrl(string url)
    {
        var buffer = new byte[GvcpConstants.UrlRegisterLength];
        Encoding.ASCII.GetBytes(url).CopyTo(buffer, 0);
        return buffer;
    }

    private static byte[] BuildReadMemAck(ushort ackId, uint address, byte[] data)
        => GvcpPackets.BuildReadMemAck(ackId, GvcpStatus.Success, address, data);

    private static byte[] PadToReadMemoryAlignment(byte[] data)
    {
        const int alignment = 4;
        var alignedLength = data.Length % alignment == 0
            ? data.Length
            : data.Length + alignment - data.Length % alignment;

        if (alignedLength == data.Length)
            return data;

        var padded = new byte[alignedLength];
        data.CopyTo(padded, 0);
        return padded;
    }

    private sealed class BrowserUserAgentXmlServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;
        private readonly CancellationTokenSource _cts = new();
        private readonly string _xml;

        private BrowserUserAgentXmlServer(TcpListener listener, string xml)
        {
            _listener = listener;
            _xml = xml;
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/camera.xml";
            _serverTask = Task.Run(ServeOnceAsync);
        }

        public string Url { get; }

        public static Task<BrowserUserAgentXmlServer> StartAsync(string xml)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new BrowserUserAgentXmlServer(listener, xml));
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Best-effort test server shutdown.
            }
            _cts.Dispose();
        }

        private async Task ServeOnceAsync()
        {
            using var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
            await using var stream = tcp.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            var request = new StringBuilder();
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(_cts.Token)))
                request.AppendLine(line);

            var hasBrowserUserAgent = request.ToString().Contains("User-Agent: Mozilla/5.0 GenICam.Net", StringComparison.OrdinalIgnoreCase);
            var body = hasBrowserUserAgent ? _xml : "Forbidden";
            var status = hasBrowserUserAgent ? "200 OK" : "403 Forbidden";
            var contentType = hasBrowserUserAgent ? "text/xml" : "text/plain";
            var bytes = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");

            await stream.WriteAsync(header, _cts.Token);
            await stream.WriteAsync(bytes, _cts.Token);
        }
    }
}
