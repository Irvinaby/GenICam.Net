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
}
