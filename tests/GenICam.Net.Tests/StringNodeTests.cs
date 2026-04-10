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
}
