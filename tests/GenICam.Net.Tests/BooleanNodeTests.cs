using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class BooleanNodeTests
{
    [Test]
    public void Value_SetTrue()
    {
        var node = new BooleanNode();
        node.Value = true;
        Assert.That(node.Value, Is.True);
    }

    [Test]
    public void ValueAsString_True_ReturnsTrue()
    {
        var node = new BooleanNode();
        node.SetValueDirect(true);
        Assert.That(node.ValueAsString, Is.EqualTo("true"));
    }

    [Test]
    public void ValueAsString_SetFromString()
    {
        var node = new BooleanNode();
        node.ValueAsString = "1";
        Assert.That(node.Value, Is.True);

        node.ValueAsString = "false";
        Assert.That(node.Value, Is.False);
    }
}
