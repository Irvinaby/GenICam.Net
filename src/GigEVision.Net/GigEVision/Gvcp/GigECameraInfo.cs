using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Information about a discovered GigE Vision camera, populated from a DISCOVERY_ACK payload.
/// </summary>
public class GigECameraInfo
{
    /// <summary>Major version of the GigE Vision spec supported by the device.</summary>
    public ushort SpecVersionMajor { get; init; }

    /// <summary>Minor version of the GigE Vision spec supported by the device.</summary>
    public ushort SpecVersionMinor { get; init; }

    /// <summary>Device mode flags.</summary>
    public uint DeviceMode { get; init; }

    /// <summary>MAC address of the device (6 bytes).</summary>
    public byte[] MacAddress { get; init; } = Array.Empty<byte>();

    /// <summary>Current IP address of the device.</summary>
    public IPAddress IpAddress { get; init; } = IPAddress.None;

    /// <summary>Current subnet mask of the device.</summary>
    public IPAddress SubnetMask { get; init; } = IPAddress.None;

    /// <summary>Current gateway address of the device.</summary>
    public IPAddress Gateway { get; init; } = IPAddress.None;

    /// <summary>Manufacturer name (up to 32 characters).</summary>
    public string ManufacturerName { get; init; } = string.Empty;

    /// <summary>Model name (up to 32 characters).</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>Device version string (up to 32 characters).</summary>
    public string DeviceVersion { get; init; } = string.Empty;

    /// <summary>Manufacturer-specific information (up to 48 characters).</summary>
    public string ManufacturerSpecificInfo { get; init; } = string.Empty;

    /// <summary>Serial number (up to 16 characters).</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>User-defined device name (up to 16 characters).</summary>
    public string UserDefinedName { get; init; } = string.Empty;

    /// <summary>
    /// Parses camera info from a discovery ACK payload (at least 248 bytes).
    /// Layout follows the GigE Vision specification Chapter 16.
    /// </summary>
    internal static GigECameraInfo FromPayload(ReadOnlySpan<byte> payload)
    {
        // Minimum discovery ACK payload is 248 bytes
        // Offsets based on the GigE Vision specification:
        // 0-1: SpecVersionMajor/Minor
        // 2-5: DeviceMode
        // 10-15: MAC (at offset 10 in some implementations, or 8+2)
        //
        // Simplified layout for our implementation:
        // Offset 0:  SpecVersionMajor (2)
        // Offset 2:  SpecVersionMinor (2)
        // Offset 4:  DeviceMode (4)
        // Offset 8:  Reserved (2)
        // Offset 10: MAC (6)
        // Offset 16: IP config (4)
        // Offset 20: Reserved (12)
        // Offset 32: CurrentIP (4)
        // Offset 36: Reserved (12)
        // Offset 48: CurrentSubnet (4)
        // Offset 52: Reserved (12)
        // Offset 64: CurrentGateway (4)
        // Offset 68: ManufacturerName (32)
        // Offset 100: ModelName (32)
        // Offset 132: DeviceVersion (32)
        // Offset 164: ManufacturerSpecificInfo (48)
        // Offset 212: SerialNumber (16)
        // Offset 228: UserDefinedName (16)
        // Total: 244 minimum

        var info = new GigECameraInfo();

        if (payload.Length < 244)
            return info;

        return new GigECameraInfo
        {
            SpecVersionMajor = BinaryPrimitives.ReadUInt16BigEndian(payload[0..]),
            SpecVersionMinor = BinaryPrimitives.ReadUInt16BigEndian(payload[2..]),
            DeviceMode = BinaryPrimitives.ReadUInt32BigEndian(payload[4..]),
            MacAddress = payload.Slice(10, 6).ToArray(),
            IpAddress = new IPAddress(BinaryPrimitives.ReadUInt32BigEndian(payload[32..])),
            SubnetMask = new IPAddress(BinaryPrimitives.ReadUInt32BigEndian(payload[48..])),
            Gateway = new IPAddress(BinaryPrimitives.ReadUInt32BigEndian(payload[64..])),
            ManufacturerName = ReadFixedString(payload.Slice(68, 32)),
            ModelName = ReadFixedString(payload.Slice(100, 32)),
            DeviceVersion = ReadFixedString(payload.Slice(132, 32)),
            ManufacturerSpecificInfo = ReadFixedString(payload.Slice(164, 48)),
            SerialNumber = ReadFixedString(payload.Slice(212, 16)),
            UserDefinedName = ReadFixedString(payload.Slice(228, 16)),
        };
    }

    /// <summary>
    /// Serialises this camera info to a discovery ACK payload (244 bytes).
    /// </summary>
    internal byte[] ToPayload()
    {
        var payload = new byte[244];

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), SpecVersionMajor);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), SpecVersionMinor);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), DeviceMode);
        MacAddress.AsSpan(0, Math.Min(6, MacAddress.Length)).CopyTo(payload.AsSpan(10));

        if (IpAddress != IPAddress.None)
            WriteIpAddress(payload.AsSpan(32), IpAddress);
        if (SubnetMask != IPAddress.None)
            WriteIpAddress(payload.AsSpan(48), SubnetMask);
        if (Gateway != IPAddress.None)
            WriteIpAddress(payload.AsSpan(64), Gateway);

        WriteFixedString(payload.AsSpan(68, 32), ManufacturerName);
        WriteFixedString(payload.AsSpan(100, 32), ModelName);
        WriteFixedString(payload.AsSpan(132, 32), DeviceVersion);
        WriteFixedString(payload.AsSpan(164, 48), ManufacturerSpecificInfo);
        WriteFixedString(payload.AsSpan(212, 16), SerialNumber);
        WriteFixedString(payload.AsSpan(228, 16), UserDefinedName);

        return payload;
    }

    private static string ReadFixedString(ReadOnlySpan<byte> data)
    {
        var endIndex = data.IndexOf((byte)0);
        var length = endIndex >= 0 ? endIndex : data.Length;
        return Encoding.ASCII.GetString(data[..length]).Trim();
    }

    private static void WriteFixedString(Span<byte> dest, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, dest.Length)).CopyTo(dest);
    }

    private static void WriteIpAddress(Span<byte> dest, IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        // Store as big-endian uint32
        if (bytes.Length == 4)
        {
            var val = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            BinaryPrimitives.WriteUInt32BigEndian(dest, val);
        }
    }
}
