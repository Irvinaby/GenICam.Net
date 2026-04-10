using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class EnumerationNodeTests
{
    private EnumerationNode CreateTestEnum()
    {
        var node = new EnumerationNode();
        node.Name = "PixelFormat";

        var entry1 = new EnumEntryNode { Name = "Mono8", Symbolic = "Mono8", NumericValue = 0 };
        var entry2 = new EnumEntryNode { Name = "Mono16", Symbolic = "Mono16", NumericValue = 1 };
        var entry3 = new EnumEntryNode { Name = "RGB8", Symbolic = "RGB8", NumericValue = 2 };

        node.AddEntry(entry1);
        node.AddEntry(entry2);
        node.AddEntry(entry3);
        node.SetValueDirect("Mono8");

        return node;
    }

    [Test]
    public void Value_SetBySymbolicName()
    {
        var node = CreateTestEnum();
        node.Value = "RGB8";

        Assert.That(node.Value, Is.EqualTo("RGB8"));
        Assert.That(node.IntValue, Is.EqualTo(2));
    }

    [Test]
    public void IntValue_SetByNumericValue()
    {
        var node = CreateTestEnum();
        node.IntValue = 1;

        Assert.That(node.Value, Is.EqualTo("Mono16"));
    }

    [Test]
    public void Value_UnknownEntry_Throws()
    {
        var node = CreateTestEnum();
        Assert.Throws<ArgumentException>(() => node.Value = "NonExistent");
    }

    [Test]
    public void Entries_ReturnsAllEntries()
    {
        var node = CreateTestEnum();
        Assert.That(node.Entries, Has.Count.EqualTo(3));
    }

    [Test]
    public void GetEntryByName_ReturnsCorrectEntry()
    {
        var node = CreateTestEnum();
        var entry = node.GetEntryByName("Mono16");

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.NumericValue, Is.EqualTo(1));
    }
}
