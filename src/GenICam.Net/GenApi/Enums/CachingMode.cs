namespace GenICam.Net.GenApi;

/// <summary>
/// Caching mode for register reads.
/// </summary>
public enum CachingMode
{
    /// <summary>No caching; always read from device.</summary>
    NoCache,

    /// <summary>Write-through caching.</summary>
    WriteThrough,

    /// <summary>Write-around caching.</summary>
    WriteAround,
}
