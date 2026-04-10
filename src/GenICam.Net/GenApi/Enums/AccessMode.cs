namespace GenICam.Net.GenApi;

/// <summary>
/// Access mode of a node as defined by the GenICam GenApi standard.
/// </summary>
public enum AccessMode
{
    /// <summary>Not implemented.</summary>
    NI,

    /// <summary>Not available.</summary>
    NA,

    /// <summary>Write only.</summary>
    WO,

    /// <summary>Read only.</summary>
    RO,

    /// <summary>Read and write.</summary>
    RW,
}
