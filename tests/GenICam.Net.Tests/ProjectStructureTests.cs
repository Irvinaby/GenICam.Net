using System.Reflection;
using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class ProjectStructureTests
{
    private static Assembly GenICamNetAssembly => typeof(NodeMap).Assembly;

    [Test]
    public void GenICamNetAssembly_DoesNotContainAnyGigEVisionTypes()
    {
        var gigEType = GenICamNetAssembly.GetTypes()
            .FirstOrDefault(t => t.Namespace?.StartsWith("GenICam.Net.GigEVision") == true);
        Assert.That(gigEType, Is.Null,
            $"GigEVision type '{gigEType?.FullName}' must not be compiled into GenICam.Net.");
    }

    [Test]
    public void GenICamNetAssembly_HasVersion1_0_0()
    {
        var version = GenICamNetAssembly.GetName().Version;
        Assert.That(version, Is.EqualTo(new Version(1, 0, 0, 0)));
    }

    [Test]
    public void GenICamGigEVisionSourceDirectory_ContainsNoCSharpFiles()
    {
        var dir = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", // up to solution root
            "src", "GenICam.Net", "GigEVision");
        if (!Directory.Exists(dir)) return; // directory deleted — pass
        var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        Assert.That(csFiles, Is.Empty,
            "No .cs files should remain under src/GenICam.Net/GigEVision/.");
    }

    [Test]
    public void GenICamTestsGigEVisionDirectory_ContainsNoCSharpFiles()
    {
        var dir = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", // up to solution root
            "tests", "GenICam.Net.Tests", "GigEVision");
        if (!Directory.Exists(dir)) return; // directory deleted — pass
        var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        Assert.That(csFiles, Is.Empty,
            "No .cs files should remain under tests/GenICam.Net.Tests/GigEVision/.");
    }
}

