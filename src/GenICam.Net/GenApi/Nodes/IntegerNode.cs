using System.Globalization;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete integer node implementation.
/// </summary>
public class IntegerNode : ValueNode, IInteger
{
    private long _value;

    public long Value
    {
        get => _value;
        set
        {
            if (value < Min || value > Max)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is out of range [{Min}, {Max}].");
            if (Increment != 0 && (value - Min) % Increment != 0)
                throw new ArgumentException($"Value {value} is not a valid increment step from {Min} with increment {Increment}.", nameof(value));
            _value = value;
            OnValueChanged();
        }
    }

    public long Min { get; internal set; } = long.MinValue;
    public long Max { get; internal set; } = long.MaxValue;
    public long Increment { get; internal set; } = 1;
    public Representation Representation { get; internal set; } = Representation.PureNumber;
    public string Unit { get; internal set; } = string.Empty;

    /// <summary>Expression formula (if this node is a SwissKnife/IntSwissKnife).</summary>
    internal string? Formula { get; set; }

    /// <summary>Variables used in the formula, mapping variable name to node name.</summary>
    internal Dictionary<string, string> FormulaVariables { get; } = new();

    public override string ValueAsString
    {
        get => Value.ToString(CultureInfo.InvariantCulture);
        set => Value = long.Parse(value, CultureInfo.InvariantCulture);
    }

    internal void SetValueDirect(long value) => _value = value;
}
