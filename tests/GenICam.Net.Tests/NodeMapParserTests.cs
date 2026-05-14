using GenICam.Net.GenApi;
using System.Buffers.Binary;

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

    [Test]
    public void Parse_IntRegWithPAddress_ReadsFromResolvedAddressNode()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="DynamicAddress">
                    <Value>0x1200</Value>
                </Integer>
                <IntReg Name="DynamicValue">
                    <pAddress>DynamicAddress</pAddress>
                    <Length>4</Length>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
            </RegisterDescription>
            """;
        var port = new ParserTestPort();
        port.Data[0x1200] = new byte[] { 0, 0, 0, 42 };

        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);
        var value = (IInteger)nodeMap.GetNode("DynamicValue")!;

        Assert.That(value.Value, Is.EqualTo(42));
    }

    [Test]
    public void Parse_UnsignedIntReg_ReadsUInt32WithoutSignExtension()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <IntReg Name="UnsignedValue">
                    <Address>0x2000</Address>
                    <Length>4</Length>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
            </RegisterDescription>
            """;
        var port = new ParserTestPort();
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, uint.MaxValue);
        port.Data[0x2000] = data;

        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);
        var value = (IInteger)nodeMap.GetNode("UnsignedValue")!;

        Assert.That(value.Value, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesArithmeticFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Width">
                    <Value>640</Value>
                </Integer>
                <Integer Name="Height">
                    <Value>480</Value>
                </Integer>
                <IntSwissKnife Name="Area">
                    <pVariable Name="W">Width</pVariable>
                    <pVariable Name="H">Height</pVariable>
                    <Formula>W * H</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var area = (IInteger)nodeMap.GetNode("Area")!;

        Assert.That(area.Value, Is.EqualTo(307200));
    }

    [Test]
    public void Parse_SwissKnife_EvaluatesFloatFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Float Name="Exposure">
                    <Value>12.5</Value>
                </Float>
                <SwissKnife Name="ExposureMs">
                    <pVariable Name="E">Exposure</pVariable>
                    <Formula>E / 1000</Formula>
                </SwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var exposureMs = (IFloat)nodeMap.GetNode("ExposureMs")!;

        Assert.That(exposureMs.Value, Is.EqualTo(0.0125).Within(0.000001));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesHexBitwiseAndTernaryFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Flags">
                    <Value>0x12</Value>
                </Integer>
                <IntSwissKnife Name="MaskedResult">
                    <pVariable Name="F">Flags</pVariable>
                    <Formula>((F &amp; 0x10) == 0x10) ? (F &lt;&lt; 1) : 0</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var result = (IInteger)nodeMap.GetNode("MaskedResult")!;

        Assert.That(result.Value, Is.EqualTo(0x24));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesLogicalFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Selector">
                    <Value>2</Value>
                </Integer>
                <Integer Name="Enable">
                    <Value>1</Value>
                </Integer>
                <IntSwissKnife Name="Available">
                    <pVariable Name="S">Selector</pVariable>
                    <pVariable Name="E">Enable</pVariable>
                    <Formula>(S &gt;= 1) &amp;&amp; (E != 0)</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var available = (IInteger)nodeMap.GetNode("Available")!;

        Assert.That(available.Value, Is.EqualTo(1));
    }

    [Test]
    public void Parse_SwissKnife_EvaluatesMathFunctionFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Float Name="GainRaw">
                    <Value>100</Value>
                </Float>
                <SwissKnife Name="GainDb">
                    <pVariable Name="G">GainRaw</pVariable>
                    <Formula>20 * LOG(G / 10)</Formula>
                </SwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var gainDb = (IFloat)nodeMap.GetNode("GainDb")!;

        Assert.That(gainDb.Value, Is.EqualTo(20).Within(0.000001));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesCameraXmlEqualityAndPowerOperators()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="SwitchTarget">
                    <Value>0</Value>
                </Integer>
                <Integer Name="Raw">
                    <Value>7</Value>
                </Integer>
                <IntSwissKnife Name="SelectorIndex">
                    <pVariable Name="SWITCH_TARGET">SwitchTarget</pVariable>
                    <Formula>( SWITCH_TARGET = 0 ) ? 3 : 4</Formula>
                </IntSwissKnife>
                <SwissKnife Name="Scaled">
                    <pVariable Name="RAW">Raw</pVariable>
                    <Formula>RAW*6.020599913/(2**14)</Formula>
                </SwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var selectorIndex = (IInteger)nodeMap.GetNode("SelectorIndex")!;
        var scaled = (IFloat)nodeMap.GetNode("Scaled")!;

        Assert.That(selectorIndex.Value, Is.EqualTo(3));
        Assert.That(scaled.Value, Is.EqualTo(7 * 6.020599913 / Math.Pow(2, 14)).Within(0.000001));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesCameraXmlNotEqualOperator()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Mono">
                    <Value>0</Value>
                </Integer>
                <IntSwissKnife Name="DynamicRange">
                    <pVariable Name="MONO">Mono</pVariable>
                    <Formula>( MONO &lt;&gt; 1 ) ? 255 : 1023</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var dynamicRange = (IInteger)nodeMap.GetNode("DynamicRange")!;

        Assert.That(dynamicRange.Value, Is.EqualTo(255));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesCameraXmlLogicalOrWithNestedConditional()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="TLParamsLocked">
                    <Value>0</Value>
                </Integer>
                <Integer Name="AcquisitionStart_Lck">
                    <Value>0</Value>
                </Integer>
                <IntSwissKnife Name="Acquisition_Locked">
                    <pVariable Name="TLPL">TLParamsLocked</pVariable>
                    <pVariable Name="LCK">AcquisitionStart_Lck</pVariable>
                    <Formula>LCK || ((TLPL = 0) ? 1 : 0)</Formula>
                </IntSwissKnife>
                <IntSwissKnife Name="TransferStart_Locked">
                    <pVariable Name="TLP">TLParamsLocked</pVariable>
                    <pVariable Name="LCK">AcquisitionStart_Lck</pVariable>
                    <Formula>(TLP = 0) || LCK</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var acquisitionLocked = (IInteger)nodeMap.GetNode("Acquisition_Locked")!;
        var transferStartLocked = (IInteger)nodeMap.GetNode("TransferStart_Locked")!;

        Assert.That(acquisitionLocked.Value, Is.EqualTo(1));
        Assert.That(transferStartLocked.Value, Is.EqualTo(1));
    }

    [Test]
    public void Parse_IntSwissKnife_EvaluatesCameraXmlNestedPixelDynamicRangeFormula()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="DataDepth_Val">
                    <Value>12</Value>
                </Integer>
                <Integer Name="PixelColor_Val">
                    <Value>1</Value>
                </Integer>
                <Integer Name="IspEnable_Val">
                    <Value>1</Value>
                </Integer>
                <Integer Name="PixelFormat_Val">
                    <Value>0x010C0004</Value>
                </Integer>
                <IntSwissKnife Name="PixelDynamicRangeMax_Val">
                    <pVariable Name="BPP">DataDepth_Val</pVariable>
                    <pVariable Name="MONO">PixelColor_Val</pVariable>
                    <pVariable Name="ISPE">IspEnable_Val</pVariable>
                    <pVariable Name="PF">PixelFormat_Val</pVariable>
                    <Formula>
                        ( ( MONO &lt;&gt; 1 ) ? 255 :
                        ( ( PF = 0x010C0004 ) || ( PF = 0x010C0026 ) || ( PF = 0x010C0027 ) || ( PF = 0x010C0028 ) || ( PF = 0x010C0029 ) ? ( 0x3FF ) :
                        ( ( BPP &gt; 12 ) &amp;&amp; ( ISPE = 1 ) ? ( 0xFFF &lt;&lt; ( BPP - 12 ) ) :
                        ( ( BPP &gt; 16 ) &amp;&amp; ( ISPE = 0 ) ? ( 0xFFFF &lt;&lt; ( BPP - 16 ) ) :
                        ( ( 1 &lt;&lt; BPP)  -1 ) ) ) ) )
                    </Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var dynamicRange = (IInteger)nodeMap.GetNode("PixelDynamicRangeMax_Val")!;

        Assert.That(dynamicRange.Value, Is.EqualTo(0x3FF));
    }

    [Test]
    public void Parse_IntSwissKnife_DoesNotResolveVariablesInUnusedConditionalBranch()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Feature">
                    <Value>10</Value>
                </Integer>
                <Integer Name="PresentValue">
                    <Value>7</Value>
                </Integer>
                <IntSwissKnife Name="OptionalBranch">
                    <pVariable Name="FEAT">Feature</pVariable>
                    <pVariable Name="PRESENT">PresentValue</pVariable>
                    <pVariable Name="MISSING">MissingOptionalNode</pVariable>
                    <Formula>(FEAT=10)?PRESENT:MISSING</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;

        var nodeMap = NodeMapParser.Parse(xml);
        var optionalBranch = (IInteger)nodeMap.GetNode("OptionalBranch")!;

        Assert.That(optionalBranch.Value, Is.EqualTo(7));
    }

    [Test]
    public void Parse_IntRegWithPAddressAndAddress_UsesAddressAsOffset()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="BaseAddress">
                    <Value>0x1000</Value>
                </Integer>
                <IntReg Name="OffsetValue">
                    <pAddress>BaseAddress</pAddress>
                    <Address>0x8</Address>
                    <Length>4</Length>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
            </RegisterDescription>
            """;
        var port = new ParserTestPort();
        port.Data[0x1008] = new byte[] { 0, 0, 0, 99 };

        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);
        var value = (IInteger)nodeMap.GetNode("OffsetValue")!;

        Assert.That(value.Value, Is.EqualTo(99));
    }

    [Test]
    public void Parse_MaskedIntRegWithBit_ReturnsBitValue()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <MaskedIntReg Name="Locked">
                    <Address>0x2000</Address>
                    <Length>4</Length>
                    <Bit>3</Bit>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </MaskedIntReg>
            </RegisterDescription>
            """;
        var port = new ParserTestPort();
        port.Data[0x2000] = new byte[] { 0, 0, 0, 8 };

        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);
        var locked = (IInteger)nodeMap.GetNode("Locked")!;

        Assert.That(locked.Value, Is.EqualTo(1));
    }

    private class ParserTestPort : IPort
    {
        public Dictionary<long, byte[]> Data { get; } = new();

        public byte[] Read(long address, long length)
            => Data.TryGetValue(address, out var data) ? data : new byte[length];

        public void Write(long address, byte[] data)
            => Data[address] = data;
    }
}
