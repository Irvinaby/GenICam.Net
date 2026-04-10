namespace GenICam.Net.GenApi;

/// <summary>
/// Base interface for all GenICam nodes as defined by the GenApi standard.
/// Every feature in a GenICam camera description file maps to a node in the node tree.
/// Nodes carry metadata (name, visibility, access mode) and can be invalidated to force re-reads.
/// </summary>
/// <remarks>
/// Nodes are typically obtained from an <see cref="INodeMap"/> after parsing a camera description XML.
/// The node hierarchy follows the GenICam GenApi specification:
/// <list type="bullet">
///   <item><see cref="IInteger"/> — 64-bit integer values with min/max/increment</item>
///   <item><see cref="IFloat"/> — double-precision floating point values</item>
///   <item><see cref="IBoolean"/> — true/false values</item>
///   <item><see cref="IString"/> — variable-length string values</item>
///   <item><see cref="IEnumeration"/> — named enumeration values</item>
///   <item><see cref="ICommand"/> — executable actions (e.g., start acquisition)</item>
///   <item><see cref="IRegister"/> — raw register byte access</item>
///   <item><see cref="ICategory"/> — grouping of related features</item>
/// </list>
/// </remarks>
public interface INode
{
    /// <summary>Unique name identifying this node within the node map (e.g., "Width", "ExposureTime").</summary>
    string Name { get; }

    /// <summary>Human-readable display name for UI presentation (e.g., "Image Width").</summary>
    string DisplayName { get; }

    /// <summary>Short description suitable for a tooltip.</summary>
    string ToolTip { get; }

    /// <summary>Detailed description of the node's purpose and behavior.</summary>
    string Description { get; }

    /// <summary>
    /// Visibility level controlling which users should see this node.
    /// <see cref="GenApi.Visibility.Beginner"/> nodes are always shown; 
    /// <see cref="GenApi.Visibility.Guru"/> nodes are only for advanced users.
    /// </summary>
    Visibility Visibility { get; }

    /// <summary>
    /// Current access mode determining whether the node can be read, written, or both.
    /// Check this before reading or writing values to avoid <see cref="InvalidOperationException"/>.
    /// </summary>
    AccessMode AccessMode { get; }

    /// <summary>Namespace indicating whether this is a standard GenICam feature or vendor-specific.</summary>
    NameSpace NameSpace { get; }

    /// <summary>Caching strategy for register reads associated with this node.</summary>
    CachingMode CachingMode { get; }

    /// <summary>Whether this node is deprecated and should be avoided in new integrations.</summary>
    bool IsDeprecated { get; }

    /// <summary>Event identifier for event-driven nodes. Empty string if not event-driven.</summary>
    string EventId { get; }

    /// <summary>
    /// Gets a named property node associated with this node.
    /// </summary>
    /// <param name="name">The property name to look up.</param>
    /// <returns>The property node, or <c>null</c> if not found.</returns>
    INode? GetProperty(string name);

    /// <summary>
    /// Invalidates any cached values, causing the next read to fetch fresh data from the device.
    /// Also propagates invalidation to dependent nodes.
    /// </summary>
    void InvalidateNode();
}
