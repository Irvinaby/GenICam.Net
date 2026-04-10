namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete string node implementation.
/// </summary>
public class StringNode : ValueNode, IString
{
    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (MaxLength > 0 && value.Length > MaxLength)
                throw new ArgumentException($"String length {value.Length} exceeds maximum {MaxLength}.", nameof(value));
            _value = value;
            OnValueChanged();
        }
    }

    public long MaxLength { get; internal set; }

    public override string ValueAsString
    {
        get => Value;
        set => Value = value;
    }

    internal void SetValueDirect(string value) => _value = value;
}
