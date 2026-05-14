using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class RealCameraXmlTests
{
    private static readonly object[] CameraXmlCases =
    [
        new object[]
        {
            "BFS-PGE-63S4M_01AC327B-20260514-213633.xml",
            "BFS_PGE_63S4",
            new Version(1, 1, 1),
            3096L,
            2094L
        },
        new object[]
        {
            "BFS-PGE-120S4M_01A6DC25-20260514-214300.xml",
            "BFS_GE_120S4",
            new Version(1, 0, 1),
            4072L,
            3046L
        }
    ];

    [TestCaseSource(nameof(CameraXmlCases))]
    public void Parse_SuppliedFlirCameraXml_LoadsExpectedCoreFeatures(
        string fileName,
        string expectedModel,
        Version expectedSchemaVersion,
        long expectedSensorWidth,
        long expectedSensorHeight)
    {
        var filePath = FindCameraXml(fileName);
        if (filePath is null)
            Assert.Ignore($"Camera XML test data '{fileName}' was not found in this workspace.");

        var nodeMap = NodeMapParser.ParseFile(filePath!);

        Assert.That(nodeMap.VendorName, Is.EqualTo("FLIR"));
        Assert.That(nodeMap.ModelName, Is.EqualTo(expectedModel));
        Assert.That(nodeMap.SchemaVersion, Is.EqualTo(expectedSchemaVersion));
        Assert.That(nodeMap.Nodes.Count, Is.GreaterThan(500));
        Assert.That(((IInteger)nodeMap.GetNode("SensorWidth")!).Value, Is.EqualTo(expectedSensorWidth));
        Assert.That(((IInteger)nodeMap.GetNode("SensorHeight")!).Value, Is.EqualTo(expectedSensorHeight));
        Assert.That(nodeMap.GetNode("Device"), Is.InstanceOf<IRegister>());
        Assert.That(nodeMap.GetNode("AcquisitionStart"), Is.InstanceOf<ICommand>());
        Assert.That(nodeMap.GetNode("PixelFormat"), Is.InstanceOf<IEnumeration>());
        Assert.That(nodeMap.GetNode("Width_Locked"), Is.InstanceOf<IInteger>());
    }

    private static string? FindCameraXml(string fileName)
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "src", "CameraViewer", "bin", "Debug", "net8.0-windows", "camera-xml", fileName);
            if (File.Exists(candidate))
                return candidate;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
