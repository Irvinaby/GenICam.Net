using System.Reflection;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;
using GenICam.Net.GenApi;

namespace GenICam.Net.Tests.GigEVision;

[TestFixture]
public class GigEVisionAssemblyTests
{
    private static Assembly GigEVisionNetAssembly => typeof(GvcpClient).Assembly;

    [Test]
    public void GigEVisionNetAssembly_HasExpectedAssemblyName()
    {
        Assert.That(GigEVisionNetAssembly.GetName().Name, Is.EqualTo("GigEVision.Net"));
    }

    [Test]
    public void GigEVisionNetAssembly_ContainsGvspReceiverType()
    {
        var type = GigEVisionNetAssembly.GetType("GenICam.Net.GigEVision.Gvsp.GvspReceiver");
        Assert.That(type, Is.Not.Null,
            "GvspReceiver must be compiled into GigEVision.Net.");
    }

    [Test]
    public void GigEVisionNetAssembly_HasVersion1_0_0()
    {
        var version = GigEVisionNetAssembly.GetName().Version;
        Assert.That(version, Is.EqualTo(new Version(1, 0, 0, 0)));
    }

    [Test]
    public void GigEVisionNetTests_CanAccessInternalNodeBase_ViaInternalsVisibleTo()
    {
        // IntegerNode.Name has internal set — this proves GenICam.Net's InternalsVisibleTo
        // grants GigEVision.Net.Tests access to GenICam.Net internals (FR-13).
        var node = new IntegerNode();
        node.Name = "TestNode";
        Assert.That(node.Name, Is.EqualTo("TestNode"));
    }
}

