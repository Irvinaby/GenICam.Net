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

    public string Value
    {
        get => _currentSymbolic;
        set
        {
            var entry = GetEntryByName(value)
                ?? throw new ArgumentException($"Unknown enumeration entry '{value}'.", nameof(value));
            _currentSymbolic = entry.Symbolic;
            OnValueChanged();
        }
    }

    public long IntValue
    {
        get
        {
            var entry = GetEntryByName(_currentSymbolic);
            return entry?.NumericValue ?? 0;
        }
        set
        {
            var entry = _entries.FirstOrDefault(e => e.NumericValue == value)
                ?? throw new ArgumentException($"No enumeration entry with numeric value {value}.", nameof(value));
            _currentSymbolic = entry.Symbolic;
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
