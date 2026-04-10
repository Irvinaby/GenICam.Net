namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// GVCP command and acknowledgment message types as defined by the GigE Vision standard.
/// </summary>
public enum GvcpCommandType : ushort
{
    /// <summary>Discovery broadcast command.</summary>
    DiscoveryCmd = 0x0002,

    /// <summary>Discovery acknowledgment.</summary>
    DiscoveryAck = 0x0003,

    /// <summary>Force IP configuration command.</summary>
    ForceIpCmd = 0x0004,

    /// <summary>Force IP configuration acknowledgment.</summary>
    ForceIpAck = 0x0005,

    /// <summary>Read register command (single 32-bit register).</summary>
    ReadRegCmd = 0x0080,

    /// <summary>Read register acknowledgment.</summary>
    ReadRegAck = 0x0081,

    /// <summary>Write register command (single 32-bit register).</summary>
    WriteRegCmd = 0x0082,

    /// <summary>Write register acknowledgment.</summary>
    WriteRegAck = 0x0083,

    /// <summary>Read memory block command.</summary>
    ReadMemCmd = 0x0084,

    /// <summary>Read memory block acknowledgment.</summary>
    ReadMemAck = 0x0085,

    /// <summary>Write memory block command.</summary>
    WriteMemCmd = 0x0086,

    /// <summary>Write memory block acknowledgment.</summary>
    WriteMemAck = 0x0087,
}
