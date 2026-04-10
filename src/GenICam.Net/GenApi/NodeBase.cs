namespace GenICam.Net.GenApi;

/// <summary>
/// Abstract base class implementing common INode properties.
/// </summary>
public abstract class NodeBase : INode
{
    public string Name { get; internal set; } = string.Empty;
    public string DisplayName { get; internal set; } = string.Empty;
    public string ToolTip { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public Visibility Visibility { get; internal set; } = Visibility.Beginner;
    public AccessMode AccessMode { get; internal set; } = AccessMode.RW;
    public NameSpace NameSpace { get; internal set; } = NameSpace.Custom;
    public CachingMode CachingMode { get; internal set; } = CachingMode.WriteThrough;
    public bool IsDeprecated { get; internal set; }
    public string EventId { get; internal set; } = string.Empty;

    /// <summary>Nodes that this node invalidates when its value changes.</summary>
    internal List<NodeBase> InvalidatedBy { get; } = [];

    public virtual INode? GetProperty(string name) => null;

    public virtual void InvalidateNode()
    {
        foreach (var node in InvalidatedBy)
        {
            node.InvalidateNode();
        }
    }
}
