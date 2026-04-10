using System.Collections.ObjectModel;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete category node that groups other nodes.
/// </summary>
public class CategoryNode : NodeBase, ICategory
{
    private readonly List<INode> _features = [];

    public ReadOnlyCollection<INode> Features => _features.AsReadOnly();

    /// <summary>Names of child features (resolved during linking).</summary>
    internal List<string> FeatureNames { get; } = [];

    internal void AddFeature(INode node) => _features.Add(node);
}
