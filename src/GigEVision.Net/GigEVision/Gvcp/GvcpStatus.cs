namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// GVCP status codes returned in acknowledgment messages.
/// </summary>
public enum GvcpStatus : ushort
{
    /// <summary>Command completed successfully.</summary>
    Success = 0x0000,

    /// <summary>The command is not implemented by the device.</summary>
    NotImplemented = 0x8001,

    /// <summary>The specified address is invalid.</summary>
    InvalidAddress = 0x8002,

    /// <summary>The target register is write-protected.</summary>
    WriteProtect = 0x8003,

    /// <summary>The address does not meet alignment requirements.</summary>
    BadAlignment = 0x8004,

    /// <summary>Access to the resource is denied.</summary>
    AccessDenied = 0x8005,

    /// <summary>The device is busy and cannot process the command.</summary>
    Busy = 0x8006,

    /// <summary>The requested packet is not available.</summary>
    PacketUnavailable = 0x800C,
}
