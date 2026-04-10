namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Exception thrown when a GVCP command fails with a non-success status code.
/// </summary>
public class GvcpException : Exception
{
    /// <summary>The GVCP status code returned by the device.</summary>
    public GvcpStatus Status { get; }

    /// <summary>
    /// Creates a new GVCP exception with the specified status code.
    /// </summary>
    public GvcpException(GvcpStatus status)
        : base($"GVCP command failed with status: {status} (0x{(ushort)status:X4})")
    {
        Status = status;
    }

    /// <summary>
    /// Creates a new GVCP exception with the specified status code and message.
    /// </summary>
    public GvcpException(GvcpStatus status, string message)
        : base(message)
    {
        Status = status;
    }
}
