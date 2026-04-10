namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a single entry within an <see cref="IEnumeration"/> node.
/// Each entry has a symbolic name and an associated numeric value.
/// </summary>
/// <remarks>
/// For example, a PixelFormat enumeration might have entries like
/// "Mono8" (value=0), "Mono16" (value=1), "RGB8" (value=2).
/// </remarks>
public interface IEnumEntry : INode
{
    /// <summary>Numeric value transmitted to/from the device register.</summary>
    long NumericValue { get; }

    /// <summary>Symbolic name used for human-readable identification (e.g., "Mono8").</summary>
    string Symbolic { get; }

    /// <summary>Whether this entry auto-resets after being set (used for trigger-like entries).</summary>
    bool IsSelfClearing { get; }
}
