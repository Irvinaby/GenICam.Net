using GenICam.Net.GenApi;
using System.Buffers.Binary;

namespace GenICam.Net.Tests;

[TestFixture]
public class CommandNodeTests
{
    [Test]
    public void Execute_WhenAvailable_DoesNotThrow()
    {
        var node = new CommandNode { AccessMode = AccessMode.WO };
        node.Name = "AcquisitionStart";

        Assert.DoesNotThrow(() => node.Execute());
    }

    [Test]
    public void Execute_WhenNotAvailable_Throws()
    {
        var node = new CommandNode { AccessMode = AccessMode.NA };
        node.Name = "AcquisitionStart";

        Assert.Throws<InvalidOperationException>(() => node.Execute());
    }

    [Test]
    public void IsDone_ReturnsTrue()
    {
        var node = new CommandNode();
        Assert.That(node.IsDone, Is.True);
    }

    [Test]
    public void IsDone_EightByteRegisterWithCommandValue_ReturnsFalse()
    {
        var nodeMap = new NodeMap();
        var register = new RegisterNode { Name = "CommandRegister", Length = 8 };
        var data = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(data, 0x0102030405060708);
        register.Set(data);
        nodeMap.AddNode(register);

        var command = new CommandNode
        {
            Name = "AcquisitionStart",
            CommandValue = 0x0102030405060708,
            RegisterNodeName = "CommandRegister",
            NodeMap = nodeMap
        };

        Assert.That(command.IsDone, Is.False);
    }
}
