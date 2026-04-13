namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// GigE Vision Control Protocol constants.
/// </summary>
public static class GvcpConstants
{
    /// <summary>GVCP uses UDP port 3956.</summary>
    public const int Port = 3956;

    /// <summary>Magic key byte at the start of every GVCP command header.</summary>
    public const byte Key = 0x42;

    /// <summary>Maximum payload size for a single GVCP read/write memory block in bytes.</summary>
    public const int MaxBlockSize = 512;

    /// <summary>Size of a GVCP command header in bytes.</summary>
    public const int CmdHeaderSize = 8;

    /// <summary>Size of a GVCP acknowledgment header in bytes.</summary>
    public const int AckHeaderSize = 8;

    /// <summary>Default timeout for GVCP responses in milliseconds.</summary>
    public const int DefaultTimeoutMs = 1000;

    /// <summary>Broadcast address for device discovery.</summary>
    public const string BroadcastAddress = "255.255.255.255";

    /// <summary>Control Channel Privilege register (exclusive access). Write 2 to take control.</summary>
    public const uint CcpRegister = 0x0A00;

    /// <summary>Stream Channel Port 0 register. Bits [31:16] = host port, bit 0 = channel enable.</summary>
    public const uint Scp0Register = 0x0D00;

    /// <summary>Stream Channel Packet Size 0 register.</summary>
    public const uint Scps0Register = 0x0D04;

    /// <summary>Stream Channel Destination Address 0 register (host IP as big-endian uint32).</summary>
    public const uint Scda0Register = 0x0D18;
}
