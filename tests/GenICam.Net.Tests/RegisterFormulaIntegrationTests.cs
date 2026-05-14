using System.Buffers.Binary;
using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class RegisterFormulaIntegrationTests
{
    [Test]
    public void IntegerWithPValue_Set_WritesLinkedIntReg()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Integer Name="Width">
                    <pValue>Width_Val</pValue>
                </Integer>
                <IntReg Name="Width_Val">
                    <Address>0x81084</Address>
                    <Length>4</Length>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
            </RegisterDescription>
            """;
        var port = new IntegrationPort();
        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);

        var width = (IInteger)nodeMap.GetNode("Width")!;
        width.Value = 1920;

        Assert.That(port.Data[0x81084], Is.EqualTo(new byte[] { 0, 0, 7, 128 }));
    }

    [Test]
    public void EnumerationWithPValue_Set_WritesLinkedIntReg()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <Enumeration Name="PixelFormat">
                    <EnumEntry Name="Mono8">
                        <Value>0x01080001</Value>
                    </EnumEntry>
                    <EnumEntry Name="Mono12Packed">
                        <Value>0x010C0006</Value>
                    </EnumEntry>
                    <pValue>PixelFormat_Val</pValue>
                </Enumeration>
                <IntReg Name="PixelFormat_Val">
                    <Address>0x81414</Address>
                    <Length>4</Length>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
            </RegisterDescription>
            """;
        var port = new IntegrationPort();
        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);

        var pixelFormat = (IEnumeration)nodeMap.GetNode("PixelFormat")!;
        pixelFormat.Value = "Mono12Packed";

        var written = BinaryPrimitives.ReadUInt32BigEndian(port.Data[0x81414]);
        Assert.That(written, Is.EqualTo(0x010C0006));
    }

    [Test]
    public void IntSwissKnife_UsesUpdatedRegisterBackedVariablesAfterPoll()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RegisterDescription ModelName="Test" VendorName="Test">
                <IntReg Name="TLParamsLocked">
                    <Address>0x9000</Address>
                    <Length>4</Length>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </IntReg>
                <MaskedIntReg Name="Width_Lck">
                    <Address>0x81080</Address>
                    <Length>4</Length>
                    <Bit>3</Bit>
                    <Sign>Unsigned</Sign>
                    <Endianess>BigEndian</Endianess>
                </MaskedIntReg>
                <IntSwissKnife Name="Width_Locked">
                    <pVariable Name="TLP">TLParamsLocked</pVariable>
                    <pVariable Name="LCK">Width_Lck</pVariable>
                    <Formula>TLP || LCK</Formula>
                </IntSwissKnife>
            </RegisterDescription>
            """;
        var port = new IntegrationPort();
        port.Data[0x9000] = new byte[] { 0, 0, 0, 0 };
        port.Data[0x81080] = new byte[] { 0, 0, 0, 0 };
        var nodeMap = NodeMapParser.Parse(xml);
        nodeMap.Connect(port);
        var locked = (IInteger)nodeMap.GetNode("Width_Locked")!;

        Assert.That(locked.Value, Is.EqualTo(0));

        port.Data[0x81080] = new byte[] { 0, 0, 0, 8 };
        nodeMap.Poll();

        Assert.That(locked.Value, Is.EqualTo(1));
    }

    private sealed class IntegrationPort : IPort
    {
        public Dictionary<long, byte[]> Data { get; } = new();

        public byte[] Read(long address, long length)
            => Data.TryGetValue(address, out var data) ? data : new byte[length];

        public void Write(long address, byte[] data)
            => Data[address] = data;
    }
}
