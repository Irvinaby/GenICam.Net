using GenICam.Net.GenApi;

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
}
