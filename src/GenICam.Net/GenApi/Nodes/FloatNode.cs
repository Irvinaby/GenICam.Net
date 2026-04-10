using System.Globalization;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete float node implementation.
/// </summary>
public class FloatNode : ValueNode, IFloat
{
    private double _value;

    public double Value
    {
        get => _value;
        set
        {
            if (value < Min || value > Max)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is out of range [{Min}, {Max}].");
            _value = value;
            OnValueChanged();
        }
    }

    public double Min { get; internal set; } = double.MinValue;
    public double Max { get; internal set; } = double.MaxValue;
    public bool HasIncrement { get; internal set; }
    public double Increment { get; internal set; }
    public Representation Representation { get; internal set; } = Representation.PureNumber;
    public string Unit { get; internal set; } = string.Empty;

    public long IntValue
    {
        get => (long)Value;
        set => Value = value;
    }

    /// <summary>Expression formula (if this node is a SwissKnife).</summary>
    internal string? Formula { get; set; }

    /// <summary>Variables used in the formula, mapping variable name to node name.</summary>
    internal Dictionary<string, string> FormulaVariables { get; } = new();

    public override string ValueAsString
    {
        get => Value.ToString("R", CultureInfo.InvariantCulture);
        set => Value = double.Parse(value, CultureInfo.InvariantCulture);
    }

    internal void SetValueDirect(double value) => _value = value;
}
