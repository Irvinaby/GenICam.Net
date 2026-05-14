using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class StringNodeTests
{
    [Test]
    public void Value_SetsSuccessfully()
    {
        var node = new StringNode();
        node.Value = "Hello";
        Assert.That(node.Value, Is.EqualTo("Hello"));
    }

    [Test]
    public void Value_ExceedsMaxLength_Throws()
    {
        var node = new StringNode { MaxLength = 5 };
        Assert.Throws<ArgumentException>(() => node.Value = "TooLongString");
    }

    [Test]
    public void ValueAsString_RoundTrips()
    {
        var node = new StringNode();
        node.ValueAsString = "Test";
        Assert.That(node.ValueAsString, Is.EqualTo("Test"));
    }

    [Test]
    public void StringReg_ConnectedToPort_ReadsNullTerminatedValue()
    {
        const string xml = """
            <RegisterDescription>
                <StringReg Name="DeviceVendorName">
                    <Address>0x1000</Address>
                    <Length>16</Length>
                </StringReg>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var port = new FakePort();
        port.SetReadData(0x1000, "TestVendor\0\0\0\0\0\0"u8.ToArray());
        nodeMap.Connect(port);

        var vendor = (IString)nodeMap.GetNode("DeviceVendorName")!;

        Assert.That(vendor.Value, Is.EqualTo("TestVendor"));
    }

    private sealed class FakePort : IPort
    {
        private readonly Dictionary<long, byte[]> _readData = [];

        public void SetReadData(long address, byte[] data) => _readData[address] = data;

        public byte[] Read(long address, long length)
        {
            var data = _readData[address];
            var result = new byte[length];
            Array.Copy(data, result, Math.Min(data.Length, result.Length));
            return result;
        }

        public void Write(long address, byte[] data) => _readData[address] = data;
    }
}
