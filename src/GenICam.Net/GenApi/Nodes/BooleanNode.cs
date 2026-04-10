namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete boolean node implementation.
/// </summary>
public class BooleanNode : ValueNode, IBoolean
{
    private bool _value;

    public bool Value
    {
        get => _value;
        set
        {
            _value = value;
            OnValueChanged();
        }
    }

    public override string ValueAsString
    {
        get => Value ? "true" : "false";
        set => Value = value is "1" or "true" or "True";
    }

    internal void SetValueDirect(bool value) => _value = value;
}
