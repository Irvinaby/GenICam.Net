using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class NodeMapTests
{
    [Test]
    public void Connect_WiresPortToRegisterNodes()
    {
        var nodeMap = new NodeMap();
        var reg = new RegisterNode { Address = 0x100, Length = 4 };
        reg.Name = "TestReg";
        nodeMap.AddNode(reg);

        var port = new TestPort();
        port.Data[0x100] = new byte[] { 1, 2, 3, 4 };

        nodeMap.Connect(port);

        var data = reg.Get(4);
        Assert.That(data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void GetNode_ExistingNode_ReturnsNode()
    {
        var nodeMap = new NodeMap();
        var node = new IntegerNode { Min = 0, Max = 100 };
        node.Name = "Width";
        nodeMap.AddNode(node);

        var result = nodeMap.GetNode("Width");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Width"));
    }

    [Test]
    public void GetNode_NonExisting_ReturnsNull()
    {
        var nodeMap = new NodeMap();
        Assert.That(nodeMap.GetNode("Missing"), Is.Null);
    }

    private class TestPort : IPort
    {
        public Dictionary<long, byte[]> Data { get; } = new();

        public byte[] Read(long address, long length)
            => Data.TryGetValue(address, out var d) ? d : new byte[length];

        public void Write(long address, byte[] data)
            => Data[address] = data;
    }
}
