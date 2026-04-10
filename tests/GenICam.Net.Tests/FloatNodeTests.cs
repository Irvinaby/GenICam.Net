using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class FloatNodeTests
{
    [Test]
    public void Value_WithinRange_SetsSuccessfully()
    {
        var node = new FloatNode { Min = 0.0, Max = 100.0 };
        node.SetValueDirect(0.0);

        node.Value = 50.5;

        Assert.That(node.Value, Is.EqualTo(50.5));
    }

    [Test]
    public void Value_OutOfRange_Throws()
    {
        var node = new FloatNode { Min = 0.0, Max = 100.0 };
        node.SetValueDirect(0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => node.Value = 200.0);
    }

    [Test]
    public void IntValue_ReturnsLongCast()
    {
        var node = new FloatNode { Min = 0.0, Max = 100.0 };
        node.SetValueDirect(42.7);

        Assert.That(node.IntValue, Is.EqualTo(42));
    }

    [Test]
    public void ValueAsString_RoundTrips()
    {
        var node = new FloatNode { Min = double.MinValue, Max = double.MaxValue };
        node.SetValueDirect(0.0);

        node.ValueAsString = "3.14";

        Assert.That(node.Value, Is.EqualTo(3.14).Within(0.001));
    }
}
