using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class IntegerNodeTests
{
    [Test]
    public void Value_WithinRange_SetsSuccessfully()
    {
        var node = new IntegerNode { Min = 0, Max = 100, Increment = 1 };
        node.SetValueDirect(0);

        node.Value = 50;

        Assert.That(node.Value, Is.EqualTo(50));
    }

    [Test]
    public void Value_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var node = new IntegerNode { Min = 0, Max = 100, Increment = 1 };
        node.SetValueDirect(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => node.Value = 200);
    }

    [Test]
    public void Value_InvalidIncrement_ThrowsArgumentException()
    {
        var node = new IntegerNode { Min = 0, Max = 100, Increment = 10 };
        node.SetValueDirect(0);

        Assert.Throws<ArgumentException>(() => node.Value = 15);
    }

    [Test]
    public void ValueAsString_RoundTrips()
    {
        var node = new IntegerNode { Min = long.MinValue, Max = long.MaxValue, Increment = 1 };
        node.SetValueDirect(0);

        node.ValueAsString = "42";

        Assert.That(node.ValueAsString, Is.EqualTo("42"));
        Assert.That(node.Value, Is.EqualTo(42));
    }

    [Test]
    public void ValueChanged_RaisedOnSet()
    {
        var node = new IntegerNode { Min = 0, Max = 100, Increment = 1 };
        node.SetValueDirect(0);
        var raised = false;
        node.ValueChanged += (_, _) => raised = true;

        node.Value = 10;

        Assert.That(raised, Is.True);
    }
}
