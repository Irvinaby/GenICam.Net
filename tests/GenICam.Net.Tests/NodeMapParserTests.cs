using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class NodeMapParserTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <RegisterDescription
            ModelName="TestCamera"
            VendorName="TestVendor"
            StandardNameSpace="GEV"
            SchemaMajorVersion="1"
            SchemaMinorVersion="1"
            SchemaSubMinorVersion="0">
            
            <Integer Name="Width">
                <DisplayName>Image Width</DisplayName>
                <Description>Width of the acquired image in pixels.</Description>
                <Visibility>Beginner</Visibility>
                <Value>1920</Value>
                <Min>1</Min>
                <Max>4096</Max>
                <Inc>1</Inc>
                <Unit>px</Unit>
                <Representation>Linear</Representation>
            </Integer>

            <Integer Name="Height">
                <DisplayName>Image Height</DisplayName>
                <Value>1080</Value>
                <Min>1</Min>
                <Max>4096</Max>
                <Inc>1</Inc>
                <Unit>px</Unit>
            </Integer>

            <Float Name="ExposureTime">
                <DisplayName>Exposure Time</DisplayName>
                <Value>10000.0</Value>
                <Min>1.0</Min>
                <Max>1000000.0</Max>
                <Unit>us</Unit>
            </Float>

            <Boolean Name="ReverseX">
                <DisplayName>Reverse X</DisplayName>
                <Value>false</Value>
            </Boolean>

            <StringReg Name="DeviceUserID">
                <DisplayName>Device User ID</DisplayName>
                <Value>MyCamera</Value>
                <MaxLength>64</MaxLength>
            </StringReg>

            <Enumeration Name="PixelFormat">
                <DisplayName>Pixel Format</DisplayName>
                <EnumEntry Name="Mono8">
                    <Value>0</Value>
                </EnumEntry>
                <EnumEntry Name="Mono16">
                    <Value>1</Value>
                </EnumEntry>
                <EnumEntry Name="RGB8">
                    <Value>2</Value>
                </EnumEntry>
                <Value>Mono8</Value>
            </Enumeration>

            <Command Name="AcquisitionStart">
                <DisplayName>Acquisition Start</DisplayName>
                <CommandValue>1</CommandValue>
            </Command>

            <Register Name="SensorRegister">
                <Address>0x1000</Address>
                <Length>4</Length>
                <Endianess>LittleEndian</Endianess>
            </Register>

            <Category Name="ImageFormatControl">
                <DisplayName>Image Format Control</DisplayName>
                <pFeature>Width</pFeature>
                <pFeature>Height</pFeature>
                <pFeature>PixelFormat</pFeature>
            </Category>
        </RegisterDescription>
        """;

    [Test]
    public void Parse_ValidXml_ReturnsNodeMap()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);

        Assert.That(nodeMap, Is.Not.Null);
        Assert.That(nodeMap.ModelName, Is.EqualTo("TestCamera"));
        Assert.That(nodeMap.VendorName, Is.EqualTo("TestVendor"));
        Assert.That(nodeMap.StandardNameSpace, Is.EqualTo("GEV"));
        Assert.That(nodeMap.SchemaVersion, Is.EqualTo(new Version(1, 1, 0)));
    }

    [Test]
    public void Parse_IntegerNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var width = nodeMap.GetNode("Width") as IInteger;

        Assert.That(width, Is.Not.Null);
        Assert.That(width!.Value, Is.EqualTo(1920));
        Assert.That(width.Min, Is.EqualTo(1));
        Assert.That(width.Max, Is.EqualTo(4096));
        Assert.That(width.Increment, Is.EqualTo(1));
        Assert.That(width.Unit, Is.EqualTo("px"));
        Assert.That(width.Representation, Is.EqualTo(Representation.Linear));
    }

    [Test]
    public void Parse_FloatNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var exposure = nodeMap.GetNode("ExposureTime") as IFloat;

        Assert.That(exposure, Is.Not.Null);
        Assert.That(exposure!.Value, Is.EqualTo(10000.0));
        Assert.That(exposure.Min, Is.EqualTo(1.0));
        Assert.That(exposure.Max, Is.EqualTo(1000000.0));
        Assert.That(exposure.Unit, Is.EqualTo("us"));
    }

    [Test]
    public void Parse_BooleanNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var reverseX = nodeMap.GetNode("ReverseX") as IBoolean;

        Assert.That(reverseX, Is.Not.Null);
        Assert.That(reverseX!.Value, Is.False);
    }

    [Test]
    public void Parse_StringNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var userId = nodeMap.GetNode("DeviceUserID") as IString;

        Assert.That(userId, Is.Not.Null);
        Assert.That(userId!.Value, Is.EqualTo("MyCamera"));
        Assert.That(userId.MaxLength, Is.EqualTo(64));
    }

    [Test]
    public void Parse_EnumerationNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var pixelFormat = nodeMap.GetNode("PixelFormat") as IEnumeration;

        Assert.That(pixelFormat, Is.Not.Null);
        Assert.That(pixelFormat!.Value, Is.EqualTo("Mono8"));
        Assert.That(pixelFormat.Entries, Has.Count.EqualTo(3));
        Assert.That(pixelFormat.IntValue, Is.EqualTo(0));
    }

    [Test]
    public void Parse_CommandNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var cmd = nodeMap.GetNode("AcquisitionStart") as ICommand;

        Assert.That(cmd, Is.Not.Null);
    }

    [Test]
    public void Parse_RegisterNode_ParsedCorrectly()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var reg = nodeMap.GetNode("SensorRegister") as IRegister;

        Assert.That(reg, Is.Not.Null);
        Assert.That(reg!.Address, Is.EqualTo(0x1000));
        Assert.That(reg.Length, Is.EqualTo(4));
    }

    [Test]
    public void Parse_CategoryNode_ResolvesFeatures()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);
        var category = nodeMap.GetNode("ImageFormatControl") as ICategory;

        Assert.That(category, Is.Not.Null);
        Assert.That(category!.Features, Has.Count.EqualTo(3));
        Assert.That(category.Features.Select(f => f.Name), Is.EquivalentTo(new[] { "Width", "Height", "PixelFormat" }));
    }

    [Test]
    public void Parse_AllNodes_CountIsCorrect()
    {
        var nodeMap = NodeMapParser.Parse(SampleXml);

        // 9 top-level nodes: Width, Height, ExposureTime, ReverseX, DeviceUserID,
        // PixelFormat, AcquisitionStart, SensorRegister, ImageFormatControl
        Assert.That(nodeMap.Nodes.Count, Is.EqualTo(9));
    }

    [Test]
    public void Parse_HexAddress_ParsedCorrectly()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Register Name="Reg1">
                    <Address>0xFF00</Address>
                    <Length>8</Length>
                </Register>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var reg = nodeMap.GetNode("Reg1") as IRegister;

        Assert.That(reg, Is.Not.Null);
        Assert.That(reg!.Address, Is.EqualTo(0xFF00));
    }
}
