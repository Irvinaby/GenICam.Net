using System.Collections.ObjectModel;
using System.Globalization;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete enumeration node implementation.
/// </summary>
public class EnumerationNode : ValueNode, IEnumeration
{
    private readonly List<IEnumEntry> _entries = [];
    private string _currentSymbolic = string.Empty;

    /// <summary>Name of the pValue reference node.</summary>
    internal string? PValueNodeName { get; set; }

    /// <summary>Resolved pValue reference node (typically an IntReg whose value maps to an entry).</summary>
    internal INode? PValueNode { get; set; }

    public string Value
    {
        get
        {
            if (PValueNode is IInteger linked)
            {
                var numericVal = linked.Value;
                var entry = _entries.FirstOrDefault(e => e.NumericValue == numericVal);
                return entry?.Symbolic ?? _currentSymbolic;
            }
            return _currentSymbolic;
        }
        set
        {
            var entry = GetEntryByName(value)
                ?? throw new ArgumentException($"Unknown enumeration entry '{value}'.", nameof(value));
            _currentSymbolic = entry.Symbolic;
            if (PValueNode is IInteger linked)
                linked.Value = entry.NumericValue;
            OnValueChanged();
        }
    }

    public long IntValue
    {
        get
        {
            if (PValueNode is IInteger linked)
                return linked.Value;
            var entry = GetEntryByName(_currentSymbolic);
            return entry?.NumericValue ?? 0;
        }
        set
        {
            var entry = _entries.FirstOrDefault(e => e.NumericValue == value)
                ?? throw new ArgumentException($"No enumeration entry with numeric value {value}.", nameof(value));
            _currentSymbolic = entry.Symbolic;
            if (PValueNode is IInteger linked)
                linked.Value = value;
            OnValueChanged();
        }
    }

    public ReadOnlyCollection<IEnumEntry> Entries => _entries.AsReadOnly();

    public IEnumEntry? GetEntryByName(string name)
        => _entries.FirstOrDefault(e => e.Symbolic == name);

    internal void AddEntry(IEnumEntry entry) => _entries.Add(entry);

    internal void SetValueDirect(string symbolic) => _currentSymbolic = symbolic;

    public override string ValueAsString
    {
        get => Value;
        set => Value = value;
    }
}
