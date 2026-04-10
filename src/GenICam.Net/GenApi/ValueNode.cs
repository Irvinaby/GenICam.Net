namespace GenICam.Net.GenApi;

/// <summary>
/// Abstract base for nodes that carry a value.
/// </summary>
public abstract class ValueNode : NodeBase, IValue
{
    public abstract string ValueAsString { get; set; }

    public event EventHandler? ValueChanged;

    protected void OnValueChanged()
    {
        ValueChanged?.Invoke(this, EventArgs.Empty);
        InvalidateNode();
    }
}
