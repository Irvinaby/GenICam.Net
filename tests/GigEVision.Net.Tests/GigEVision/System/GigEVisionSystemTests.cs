using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;

namespace GenICam.Net.Tests.GigEVision.System;

[TestFixture]
[Category("System")]
[Explicit("Requires a physical GigE Vision camera on the network. Set GENICAM_SYSTEM_TESTS=1 to enable.")]
public class GigEVisionSystemTests
{
    private const string EnableSystemTestsVariable = "GENICAM_SYSTEM_TESTS";
    private const string CameraIpVariable = "GENICAM_CAMERA_IP";
    private const string XmlPathVariable = "GENICAM_XML_PATH";
    private const string DiscoveryTimeoutVariable = "GENICAM_DISCOVERY_TIMEOUT_MS";
    private const string StreamPortVariable = "GENICAM_STREAM_PORT";
    private const string StreamTimeoutVariable = "GENICAM_STREAM_TIMEOUT_MS";
    private const string StreamDestinationAddressRegisterVariable = "GENICAM_STREAM_DESTINATION_ADDRESS_REGISTER";

    private const uint XmlUrlRegisterAddress = 0x00000200;
    private const int XmlUrlRegisterLength = 512;
    private const uint ControlChannelPrivilegeRegister = 0x00000A00;
    private const uint StreamChannelPortRegister = 0x00000D00;
    private const uint StreamChannelPacketSizeRegister = 0x00000D04;
    private const uint StreamChannelDestinationAddressRegister = 0x00000D18;
    private const uint ExclusiveControlPrivilege = 0x00000002;
    private const uint ControlPrivilege = 0x00000001;
    private const int DefaultDiscoveryTimeoutMs = 3000;
    private const int DefaultStreamPort = 50000;
    private const int DefaultStreamTimeoutMs = 5000;

    [SetUp]
    public void RequireSystemTestOptIn()
    {
        if (!IsEnabled(EnableSystemTestsVariable))
            Assert.Ignore($"Set {EnableSystemTestsVariable}=1 and run explicit NUnit tests to exercise a physical GigE Vision camera.");
    }

    [Test]
    public async Task DiscoverAndConnectToGigEVisionCamera_VerifiesBootstrapAccess()
    {
        var camera = await ResolveCameraAsync();

        using var client = CreateClient(camera.IpAddress);
        var xmlUrlBytes = await client.ReadMemoryAsync(XmlUrlRegisterAddress, XmlUrlRegisterLength);
        var xmlUrl = DecodeBootstrapString(xmlUrlBytes);

        Assert.Multiple(() =>
        {
            Assert.That(camera.IpAddress, Is.Not.EqualTo(IPAddress.None));
            Assert.That(camera.ManufacturerName, Is.Not.Empty);
            Assert.That(camera.ModelName, Is.Not.Empty);
            Assert.That(xmlUrl, Is.Not.Empty);
            Assert.That(xmlUrl, Does.Contain(":").Or.Contain(";"), "The bootstrap XML URL register should contain a GenICam XML URL descriptor.");
        });
    }

    [Test]
    public async Task GenICamNodeMap_GetAndSetFeatureNodes_VerifiesRoundTripValues()
    {
        var camera = await ResolveCameraAsync();
        using var client = CreateClient(camera.IpAddress);
        await TakeControlIfAvailableAsync(client);

        var nodeMap = await LoadNodeMapAsync(client);
        nodeMap.Connect(new GigEPort(client));

        var readableInteger = FindReadableInteger(nodeMap);
        Assert.That(readableInteger, Is.Not.Null, "Expected at least one readable integer node in the camera XML.");
        Assert.That(readableInteger!.Value, Is.InRange(readableInteger.Min, readableInteger.Max));

        var writableInteger = FindWritableInteger(nodeMap);
        Assert.That(writableInteger, Is.Not.Null, "Expected a writable standard integer node such as Width, Height, OffsetX, or OffsetY.");

        var originalValue = writableInteger!.Value;
        writableInteger.Value = originalValue;
        nodeMap.Poll();

        Assert.That(writableInteger.Value, Is.EqualTo(originalValue), $"Node {writableInteger.Name} should round-trip the value written through GVCP.");

        var readableEnumeration = FindReadableEnumeration(nodeMap);
        if (readableEnumeration is not null)
        {
            Assert.That(readableEnumeration.Value, Is.Not.Empty);
            Assert.That(readableEnumeration.Entries.Select(entry => entry.Symbolic), Does.Contain(readableEnumeration.Value));
        }
    }

    [Test]
    public async Task AcquireImageOverGvsp_VerifiesReceivedFrame()
    {
        var camera = await ResolveCameraAsync();
        var localAddress = ResolveLocalAddressFor(camera.IpAddress);
        var streamPort = GetInt(StreamPortVariable, DefaultStreamPort);
        var streamTimeout = GetInt(StreamTimeoutVariable, DefaultStreamTimeoutMs);
        var destinationAddressRegister = GetUInt(StreamDestinationAddressRegisterVariable, StreamChannelDestinationAddressRegister);

        using var client = CreateClient(camera.IpAddress);
        await TakeControlIfAvailableAsync(client);

        var nodeMap = await LoadNodeMapAsync(client);
        nodeMap.Connect(new GigEPort(client));

        using var streamUdpClient = new UdpClient(streamPort);
        var streamTransport = new CountingUdpTransport(new UdpTransportAdapter(streamUdpClient));
        using var receiver = new GvspReceiver(streamTransport);

        var frameCompletion = new TaskCompletionSource<GvspFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.FrameReceived += (_, frame) => frameCompletion.TrySetResult(frame);

        using var receiveCts = new CancellationTokenSource(streamTimeout);
        var receiveTask = receiver.StartAsync(receiveCts.Token);

        await ConfigureStreamAsync(client, nodeMap, localAddress, streamPort, destinationAddressRegister);

        try
        {
            TrySetEnumeration(nodeMap, "AcquisitionMode", "Continuous");
            TrySetEnumeration(nodeMap, "TriggerMode", "Off");
            ExecuteIfPresent(nodeMap, "AcquisitionStart");
            var frame = await WaitForFrameAsync(frameCompletion.Task, streamTimeout, streamTransport);

            Assert.Multiple(() =>
            {
                Assert.That(frame.PayloadType, Is.EqualTo(GvspPayloadType.Image));
                Assert.That(frame.SizeX, Is.GreaterThan(0));
                Assert.That(frame.SizeY, Is.GreaterThan(0));
                Assert.That(frame.Data, Is.Not.Empty);
                Assert.That(frame.Data.Any(value => value != 0), Is.True, "The acquired image payload should contain non-zero pixel data.");
            });
        }
        finally
        {
            TryExecute(nodeMap, "AcquisitionStop");
            receiveCts.Cancel();
            await receiveTask;
        }
    }

    private static async Task<GigECameraInfo> ResolveCameraAsync()
    {
        var cameraIp = Environment.GetEnvironmentVariable(CameraIpVariable);
        if (IPAddress.TryParse(cameraIp, out var ipAddress))
        {
            return new GigECameraInfo
            {
                IpAddress = ipAddress,
                ManufacturerName = "Configured",
                ModelName = "GigE Vision camera",
            };
        }

        using var discovery = new GigEDiscovery(new UdpTransportAdapter());
        var cameras = await discovery.DiscoverAsync(GetInt(DiscoveryTimeoutVariable, DefaultDiscoveryTimeoutMs));

        Assert.That(cameras, Is.Not.Empty, $"No GigE Vision cameras were discovered. Set {CameraIpVariable} to target a known camera IP.");
        return cameras[0];
    }

    private static GvcpClient CreateClient(IPAddress ipAddress)
        => new(new UdpTransportAdapter(), new IPEndPoint(ipAddress, GvcpConstants.Port));

    private static async Task<NodeMap> LoadNodeMapAsync(GvcpClient client)
    {
        var configuredXmlPath = Environment.GetEnvironmentVariable(XmlPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredXmlPath))
            return NodeMapParser.ParseFile(configuredXmlPath);

        var xmlUrlBytes = await client.ReadMemoryAsync(XmlUrlRegisterAddress, XmlUrlRegisterLength);
        var xmlUrl = DecodeBootstrapString(xmlUrlBytes);
        TestContext.Out.WriteLine($"GenICam XML URL: {xmlUrl}");

        var xmlLocation = ParseXmlLocation(xmlUrl);
        TestContext.Out.WriteLine($"GenICam XML location: file={xmlLocation.FileName}, address=0x{xmlLocation.Address:X8}, length={xmlLocation.Length}");
        var xmlBytes = await ReadMemoryInChunksAsync(client, xmlLocation.Address, xmlLocation.Length);
        var xml = DecodeXml(xmlBytes, xmlLocation.FileName);

        return NodeMapParser.Parse(xml);
    }

    private static async Task TakeControlIfAvailableAsync(GvcpClient client)
    {
        try
        {
            await client.WriteRegisterAsync(ControlChannelPrivilegeRegister, ExclusiveControlPrivilege);
        }
        catch (GvcpException)
        {
            await client.WriteRegisterAsync(ControlChannelPrivilegeRegister, ControlPrivilege);
        }
    }

    private static async Task ConfigureStreamAsync(GvcpClient client, NodeMap nodeMap, IPAddress localAddress, int streamPort, uint destinationAddressRegister)
    {
        var destinationAddress = IpAddressToBigEndianUInt32(localAddress);

        TestContext.Out.WriteLine($"Configuring GVSP stream: localAddress={localAddress}, streamPort={streamPort}, destination=0x{destinationAddress:X8}");

        var configuredViaNodeMap =
            TrySetInteger(nodeMap, "GevSCPHostPort", streamPort) &
            TrySetInteger(nodeMap, "GevSCDA", destinationAddress);

        TrySetInteger(nodeMap, "GevSCPSPacketSize", 1500);

        if (configuredViaNodeMap)
            return;

        await client.WriteRegisterAsync(StreamChannelPortRegister, (uint)streamPort);
        await client.WriteRegisterAsync(StreamChannelPacketSizeRegister, 1500);
        await client.WriteRegisterAsync(destinationAddressRegister, destinationAddress);
    }

    private static async Task<byte[]> ReadMemoryInChunksAsync(GvcpClient client, uint address, int length)
    {
        var result = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var chunkLength = Math.Min(GvcpConstants.MaxBlockSize, length - offset);
            var chunk = await client.ReadMemoryAsync(address + (uint)offset, chunkLength);
            Assert.That(chunk, Has.Length.EqualTo(chunkLength), $"Short GVCP memory read at 0x{address + (uint)offset:X8}.");

            chunk.CopyTo(result, offset);
            offset += chunkLength;
        }

        return result;
    }

    private static IPAddress ResolveLocalAddressFor(IPAddress remoteAddress)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(remoteAddress, GvcpConstants.Port);
        return ((IPEndPoint)socket.LocalEndPoint!).Address;
    }

    private static IInteger? FindReadableInteger(NodeMap nodeMap)
        => PreferredNodes<IInteger>(nodeMap, "Width", "Height", "OffsetX", "OffsetY", "PayloadSize")
            .FirstOrDefault(node => node.AccessMode is AccessMode.RO or AccessMode.RW);

    private static IInteger? FindWritableInteger(NodeMap nodeMap)
        => PreferredNodes<IInteger>(nodeMap, "Width", "Height", "OffsetX", "OffsetY")
            .FirstOrDefault(node => node.AccessMode == AccessMode.RW && IsSafeIntegerRange(node));

    private static IEnumeration? FindReadableEnumeration(NodeMap nodeMap)
        => PreferredNodes<IEnumeration>(nodeMap, "PixelFormat", "AcquisitionMode", "TriggerMode")
            .FirstOrDefault(node => node.AccessMode is AccessMode.RO or AccessMode.RW && node.Entries.Count > 0);

    private static IEnumerable<TNode> PreferredNodes<TNode>(NodeMap nodeMap, params string[] preferredNames)
        where TNode : class, INode
    {
        foreach (var name in preferredNames)
        {
            if (nodeMap.GetNode(name) is TNode node)
                yield return node;
        }

        foreach (var node in nodeMap.Nodes.OfType<TNode>())
        {
            if (!preferredNames.Contains(node.Name, StringComparer.Ordinal))
                yield return node;
        }
    }

    private static bool IsSafeIntegerRange(IInteger node)
        => node.Min <= node.Value && node.Value <= node.Max && node.Increment >= 0 && node.Max > node.Min;

    private static void ExecuteIfPresent(NodeMap nodeMap, string commandName)
    {
        var command = nodeMap.GetNode(commandName) as ICommand;
        Assert.That(command, Is.Not.Null, $"The camera XML must expose a {commandName} command to run this acquisition test.");
        command!.Execute();
    }

    private static void TryExecute(NodeMap nodeMap, string commandName)
    {
        try
        {
            (nodeMap.GetNode(commandName) as ICommand)?.Execute();
        }
        catch
        {
            // Cleanup commands should not hide the acquisition assertion.
        }
    }

    private static void TrySetEnumeration(NodeMap nodeMap, string nodeName, string value)
    {
        try
        {
            if (nodeMap.GetNode(nodeName) is IEnumeration node && node.AccessMode == AccessMode.RW)
                node.Value = value;
        }
        catch
        {
            // Optional camera state defaults vary by model; unsupported values are not fatal here.
        }
    }

    private static bool TrySetInteger(NodeMap nodeMap, string nodeName, long value)
    {
        try
        {
            if (nodeMap.GetNode(nodeName) is not IInteger node || node.AccessMode != AccessMode.RW)
                return false;

            node.Value = ClampToIncrement(value, node);
            TestContext.Out.WriteLine($"Set {nodeName}={node.Value}");
            return true;
        }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"Could not set {nodeName}: {ex.Message}");
            return false;
        }
    }

    private static long ClampToIncrement(long value, IInteger node)
    {
        var clamped = Math.Min(Math.Max(value, node.Min), node.Max);
        if (node.Increment <= 1)
            return clamped;

        return node.Min + ((clamped - node.Min) / node.Increment) * node.Increment;
    }

    private static async Task<GvspFrame> WaitForFrameAsync(Task<GvspFrame> frameTask, int timeoutMs, CountingUdpTransport streamTransport)
    {
        try
        {
            return await frameTask.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Timed out waiting for a complete GVSP frame after {timeoutMs}ms. Received {streamTransport.ReceivedPacketCount} UDP packet(s) on the stream port. Samples: {streamTransport.PacketSamples}",
                ex);
        }
    }

    private static string DecodeBootstrapString(byte[] bytes)
    {
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;

        return Encoding.ASCII.GetString(bytes, 0, length).Trim();
    }

    private static XmlLocation ParseXmlLocation(string xmlUrl)
    {
        var parts = xmlUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.That(parts, Has.Length.GreaterThanOrEqualTo(3), $"Unsupported GenICam XML bootstrap URL '{xmlUrl}'.");
        Assert.That(parts[0], Does.StartWith("Local:"), "Only local camera XML bootstrap URLs are supported by this system test.");

        return new XmlLocation(parts[0]["Local:".Length..], ParseXmlUrlUInt32(parts[1]), (int)ParseXmlUrlUInt32(parts[2]));
    }

    private static string DecodeXml(byte[] bytes, string fileName)
    {
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault(item => item.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                ?? archive.Entries.FirstOrDefault(item => item.Length > 0);
            Assert.That(entry, Is.Not.Null, "The camera XML ZIP did not contain a readable entry.");

            using var stream = entry!.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    private static uint ParseXmlUrlUInt32(string value)
    {
        value = value.Trim();
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(value[2..], 16)
            : Convert.ToUInt32(value, 16);
    }

    private static uint ParseUInt32Flexible(string value)
    {
        value = value.Trim();
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(value[2..], 16)
            : Convert.ToUInt32(value);
    }

    private static uint IpAddressToBigEndianUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        Assert.That(bytes, Has.Length.EqualTo(4), "GigE Vision streaming requires an IPv4 local address.");

        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static bool IsEnabled(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return value is "1" or "true" or "TRUE" or "yes" or "YES";
    }

    private static int GetInt(string variableName, int defaultValue)
        => int.TryParse(Environment.GetEnvironmentVariable(variableName), out var value) ? value : defaultValue;

    private static uint GetUInt(string variableName, uint defaultValue)
    {
        var rawValue = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(rawValue))
            return defaultValue;

        return ParseUInt32Flexible(rawValue);
    }

    private sealed record XmlLocation(string FileName, uint Address, int Length);

    private sealed class CountingUdpTransport(IUdpTransport inner) : IUdpTransport
    {
        public int ReceivedPacketCount { get; private set; }
        public string PacketSamples => string.Join(" | ", _packetSamples);
        private readonly List<string> _packetSamples = [];

        public Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default)
            => inner.SendAsync(data, endPoint, cancellationToken);

        public async Task<GenICam.Net.GigEVision.Gvcp.UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            var result = await inner.ReceiveAsync(cancellationToken);
            ReceivedPacketCount++;
            if (_packetSamples.Count < 5)
                _packetSamples.Add(ToHex(result.Buffer.AsSpan(0, Math.Min(result.Buffer.Length, 16))) + $" ({result.Buffer.Length} bytes)");
            return result;
        }

        public void EnableBroadcast() => inner.EnableBroadcast();

        public void Dispose() => inner.Dispose();

        private static string ToHex(ReadOnlySpan<byte> data)
            => string.Join(" ", data.ToArray().Select(value => value.ToString("X2")));
    }
}
