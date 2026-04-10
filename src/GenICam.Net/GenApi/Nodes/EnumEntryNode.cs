namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete enumeration entry implementation.
/// </summary>
public class EnumEntryNode : NodeBase, IEnumEntry
{
    public long NumericValue { get; internal set; }
    public string Symbolic { get; internal set; } = string.Empty;
    public bool IsSelfClearing { get; internal set; }
}
