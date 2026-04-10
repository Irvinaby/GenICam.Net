namespace GenICam.Net.GenApi;

/// <summary>
/// Provides access to the complete node tree parsed from a GenICam camera description XML.
/// The node map is the central entry point for interacting with camera features.
/// </summary>
/// <remarks>
/// <para><b>Typical workflow:</b></para>
/// <list type="number">
///   <item>Parse a camera XML: <c>var nodeMap = NodeMapParser.ParseFile("camera.xml");</c></item>
///   <item>Connect a transport: <c>nodeMap.Connect(myGigEPort);</c></item>
///   <item>Read features: <c>var width = (IInteger)nodeMap.GetNode("Width");</c></item>
///   <item>Write features: <c>width.Value = 1920;</c></item>
///   <item>Execute commands: <c>((ICommand)nodeMap.GetNode("AcquisitionStart")).Execute();</c></item>
/// </list>
/// </remarks>
public interface INodeMap
{
    /// <summary>
    /// Retrieves a node by its unique name.
    /// </summary>
    /// <param name="name">The node name as declared in the camera XML (e.g., "Width", "PixelFormat").</param>
    /// <returns>The node instance, or <c>null</c> if no node with that name exists.</returns>
    INode? GetNode(string name);

    /// <summary>Read-only list of all nodes in the map.</summary>
    IReadOnlyList<INode> Nodes { get; }

    /// <summary>Device name or product GUID from the XML description.</summary>
    string DeviceName { get; }

    /// <summary>Camera model name from the XML description.</summary>
    string ModelName { get; }

    /// <summary>Camera vendor name from the XML description.</summary>
    string VendorName { get; }

    /// <summary>Standard namespace URI (e.g., "GEV" for GigE Vision).</summary>
    string StandardNameSpace { get; }

    /// <summary>GenICam schema version (major.minor.subminor) of the parsed XML.</summary>
    Version SchemaVersion { get; }

    /// <summary>
    /// Connects the node map to a transport layer port, enabling actual register read/write
    /// with the physical device. All register and command nodes are wired to the given port.
    /// </summary>
    /// <param name="port">The transport layer port implementation.</param>
    void Connect(IPort port);

    /// <summary>
    /// Forces all nodes to invalidate their cached values, causing the next access to
    /// re-read from the device. Useful after external changes to the device state.
    /// </summary>
    void Poll();
}
