using GenICam.Net.GenApi;

namespace CameraViewer.Demo;

/// <summary>
/// Creates a synthetic <see cref="NodeMap"/> for UI development and testing without a real camera.
/// Uses <see cref="NodeMapParser"/> with an embedded GenICam XML description.
/// </summary>
internal static class DemoNodeMapFactory
{
    /// <summary>Creates a demo node map with representative camera features.</summary>
    public static NodeMap Create() => NodeMapParser.Parse(DemoXml);

    private const string DemoXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <RegisterDescription
            ModelName="Demo-5MP"
            VendorName="Demo Vendor"
            StandardNameSpace="GEV"
            SchemaMajorVersion="1"
            SchemaMinorVersion="1"
            SchemaSubMinorVersion="0"
            MajorVersion="1"
            MinorVersion="0"
            SubMinorVersion="0"
            ProductGuid="{00000000-0000-0000-0000-000000000001}"
            VersionGuid="{00000000-0000-0000-0000-000000000002}"
            xmlns="http://www.genicam.org/GenApi/Version_1_1"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

          <!-- ── Image Format Control ─────────────────────────────── -->
          <Category Name="ImageFormatControl">
            <DisplayName>Image Format Control</DisplayName>
            <pFeature>Width</pFeature>
            <pFeature>Height</pFeature>
            <pFeature>PixelFormat</pFeature>
            <pFeature>OffsetX</pFeature>
            <pFeature>OffsetY</pFeature>
          </Category>

          <Integer Name="Width">
            <DisplayName>Width</DisplayName>
            <ToolTip>Width of the capture region in pixels.</ToolTip>
            <Visibility>Beginner</Visibility>
            <Value>1920</Value>
            <Min>64</Min>
            <Max>4096</Max>
            <Inc>16</Inc>
            <Unit>px</Unit>
          </Integer>

          <Integer Name="Height">
            <DisplayName>Height</DisplayName>
            <ToolTip>Height of the capture region in pixels.</ToolTip>
            <Visibility>Beginner</Visibility>
            <Value>1080</Value>
            <Min>64</Min>
            <Max>3000</Max>
            <Inc>16</Inc>
            <Unit>px</Unit>
          </Integer>

          <Enumeration Name="PixelFormat">
            <DisplayName>Pixel Format</DisplayName>
            <Visibility>Beginner</Visibility>
            <EnumEntry Name="Mono8"><Symbolic>Mono8</Symbolic><Value>0x01080001</Value></EnumEntry>
            <EnumEntry Name="Mono16"><Symbolic>Mono16</Symbolic><Value>0x01100007</Value></EnumEntry>
            <EnumEntry Name="BayerRG8"><Symbolic>BayerRG8</Symbolic><Value>0x01080009</Value></EnumEntry>
            <Value>Mono8</Value>
          </Enumeration>

          <Integer Name="OffsetX">
            <DisplayName>Offset X</DisplayName>
            <Visibility>Expert</Visibility>
            <Value>0</Value>
            <Min>0</Min>
            <Max>3968</Max>
            <Inc>16</Inc>
            <Unit>px</Unit>
          </Integer>

          <Integer Name="OffsetY">
            <DisplayName>Offset Y</DisplayName>
            <Visibility>Expert</Visibility>
            <Value>0</Value>
            <Min>0</Min>
            <Max>2920</Max>
            <Inc>16</Inc>
            <Unit>px</Unit>
          </Integer>

          <!-- ── Acquisition Control ──────────────────────────────── -->
          <Category Name="AcquisitionControl">
            <DisplayName>Acquisition Control</DisplayName>
            <pFeature>AcquisitionMode</pFeature>
            <pFeature>AcquisitionStart</pFeature>
            <pFeature>AcquisitionStop</pFeature>
            <pFeature>ExposureTime</pFeature>
            <pFeature>ExposureAuto</pFeature>
          </Category>

          <Enumeration Name="AcquisitionMode">
            <DisplayName>Acquisition Mode</DisplayName>
            <Visibility>Beginner</Visibility>
            <EnumEntry Name="Continuous"><Symbolic>Continuous</Symbolic><Value>0</Value></EnumEntry>
            <EnumEntry Name="SingleFrame"><Symbolic>SingleFrame</Symbolic><Value>1</Value></EnumEntry>
            <EnumEntry Name="MultiFrame"><Symbolic>MultiFrame</Symbolic><Value>2</Value></EnumEntry>
            <Value>Continuous</Value>
          </Enumeration>

          <Command Name="AcquisitionStart">
            <DisplayName>Acquisition Start</DisplayName>
            <Visibility>Beginner</Visibility>
            <CommandValue>1</CommandValue>
          </Command>

          <Command Name="AcquisitionStop">
            <DisplayName>Acquisition Stop</DisplayName>
            <Visibility>Beginner</Visibility>
            <CommandValue>1</CommandValue>
          </Command>

          <Float Name="ExposureTime">
            <DisplayName>Exposure Time</DisplayName>
            <ToolTip>Exposure duration in microseconds.</ToolTip>
            <Visibility>Beginner</Visibility>
            <Value>10000.0</Value>
            <Min>10.0</Min>
            <Max>1000000.0</Max>
            <Unit>µs</Unit>
          </Float>

          <Enumeration Name="ExposureAuto">
            <DisplayName>Exposure Auto</DisplayName>
            <Visibility>Beginner</Visibility>
            <EnumEntry Name="Off"><Symbolic>Off</Symbolic><Value>0</Value></EnumEntry>
            <EnumEntry Name="Once"><Symbolic>Once</Symbolic><Value>1</Value></EnumEntry>
            <EnumEntry Name="Continuous"><Symbolic>Continuous</Symbolic><Value>2</Value></EnumEntry>
            <Value>Off</Value>
          </Enumeration>

          <!-- ── Analog Control ───────────────────────────────────── -->
          <Category Name="AnalogControl">
            <DisplayName>Analog Control</DisplayName>
            <pFeature>Gain</pFeature>
            <pFeature>GainAuto</pFeature>
            <pFeature>BlackLevel</pFeature>
          </Category>

          <Float Name="Gain">
            <DisplayName>Gain</DisplayName>
            <ToolTip>Amplification applied to pixel values (dB).</ToolTip>
            <Visibility>Beginner</Visibility>
            <Value>0.0</Value>
            <Min>0.0</Min>
            <Max>24.0</Max>
            <Unit>dB</Unit>
          </Float>

          <Enumeration Name="GainAuto">
            <DisplayName>Gain Auto</DisplayName>
            <Visibility>Beginner</Visibility>
            <EnumEntry Name="Off"><Symbolic>Off</Symbolic><Value>0</Value></EnumEntry>
            <EnumEntry Name="Once"><Symbolic>Once</Symbolic><Value>1</Value></EnumEntry>
            <EnumEntry Name="Continuous"><Symbolic>Continuous</Symbolic><Value>2</Value></EnumEntry>
            <Value>Off</Value>
          </Enumeration>

          <Float Name="BlackLevel">
            <DisplayName>Black Level</DisplayName>
            <Visibility>Expert</Visibility>
            <Value>0.0</Value>
            <Min>0.0</Min>
            <Max>4095.0</Max>
          </Float>

          <!-- ── Transport Layer Control ──────────────────────────── -->
          <Category Name="TransportLayerControl">
            <DisplayName>Transport Layer Control</DisplayName>
            <pFeature>GevSCPSPacketSize</pFeature>
            <pFeature>GevSCPD</pFeature>
          </Category>

          <Integer Name="GevSCPSPacketSize">
            <DisplayName>GEV SCPS Packet Size</DisplayName>
            <Visibility>Expert</Visibility>
            <Value>1500</Value>
            <Min>220</Min>
            <Max>9000</Max>
            <Inc>4</Inc>
            <Unit>bytes</Unit>
          </Integer>

          <Integer Name="GevSCPD">
            <DisplayName>GEV Packet Delay</DisplayName>
            <Visibility>Guru</Visibility>
            <Value>0</Value>
            <Min>0</Min>
            <Max>65535</Max>
            <Inc>1</Inc>
          </Integer>

          <!-- ── Device Control ───────────────────────────────────── -->
          <Category Name="DeviceControl">
            <DisplayName>Device Control</DisplayName>
            <pFeature>DeviceModelName</pFeature>
            <pFeature>DeviceVendorName</pFeature>
            <pFeature>DeviceTemperature</pFeature>
          </Category>

          <String Name="DeviceModelName">
            <DisplayName>Device Model Name</DisplayName>
            <Visibility>Beginner</Visibility>
            <AccessMode>RO</AccessMode>
            <Value>Demo-5MP</Value>
          </String>

          <String Name="DeviceVendorName">
            <DisplayName>Device Vendor Name</DisplayName>
            <Visibility>Beginner</Visibility>
            <AccessMode>RO</AccessMode>
            <Value>Demo Vendor</Value>
          </String>

          <Float Name="DeviceTemperature">
            <DisplayName>Device Temperature</DisplayName>
            <Visibility>Expert</Visibility>
            <AccessMode>RO</AccessMode>
            <Value>42.5</Value>
            <Min>0.0</Min>
            <Max>100.0</Max>
            <Unit>°C</Unit>
          </Float>

        </RegisterDescription>
        """;
}
