namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete implementation of <see cref="INodeMap"/> that holds all parsed GenICam nodes
/// and provides the primary API for interacting with camera features.
/// </summary>
/// <remarks>
/// <para><b>Creating a NodeMap:</b> Use <see cref="NodeMapParser"/> to parse a camera XML description.</para>
/// <para><b>Connecting to hardware:</b> Call <see cref="Connect"/> with an <see cref="IPort"/> implementation
/// for your transport layer (GigE Vision, USB3 Vision, etc.).</para>
///
/// <para><b>Reading and writing feature values:</b></para>
/// <code>
/// var nodeMap = NodeMapParser.ParseFile("camera.xml");
/// nodeMap.Connect(myPort);
///
/// // Read/write integer features (e.g., Width, Height)
/// var width = (IInteger)nodeMap.GetNode("Width")!;
/// Console.WriteLine($"Width = {width.Value} (range: {width.Min}..{width.Max}, step: {width.Increment})");
/// width.Value = 1920;
///
/// // Read/write float features (e.g., ExposureTime, Gain)
/// var exposure = (IFloat)nodeMap.GetNode("ExposureTime")!;
/// exposure.Value = 15000.0; // 15 ms
///
/// // Read/write boolean features (e.g., ReverseX)
/// var reverseX = (IBoolean)nodeMap.GetNode("ReverseX")!;
/// reverseX.Value = true;
///
/// // Read/write enumeration features (e.g., PixelFormat)
/// var pixelFormat = (IEnumeration)nodeMap.GetNode("PixelFormat")!;
/// pixelFormat.Value = "Mono8";           // by symbolic name
/// pixelFormat.IntValue = 1;               // by numeric value
/// foreach (var entry in pixelFormat.Entries)
///     Console.WriteLine($"  {entry.Symbolic} = {entry.NumericValue}");
///
/// // Read/write string features (e.g., DeviceUserID)
/// var userId = (IString)nodeMap.GetNode("DeviceUserID")!;
/// userId.Value = "MyCamera01";
///
/// // Execute commands (e.g., AcquisitionStart, AcquisitionStop)
/// var startCmd = (ICommand)nodeMap.GetNode("AcquisitionStart")!;
/// startCmd.Execute();
/// while (!startCmd.IsDone) { /* poll */ }
///
/// // Raw register access (advanced)
/// var register = (IRegister)nodeMap.GetNode("SensorRegister")!;
/// byte[] data = register.Get(register.Length);
/// register.Set(new byte[] { 0x01, 0x02, 0x03, 0x04 });
///
/// // Browse the feature tree via categories
/// var category = (ICategory)nodeMap.GetNode("ImageFormatControl")!;
/// foreach (var feature in category.Features)
///     Console.WriteLine($"  {feature.Name}: {feature.DisplayName}");
///
/// // Subscribe to value changes
/// ((IValue)nodeMap.GetNode("Width")!).ValueChanged += (s, e) =&gt;
///     Console.WriteLine("Width changed!");
///
/// // Invalidate all cached values (re-read from device)
/// nodeMap.Poll();
/// </code>
/// </remarks>
public class NodeMap : INodeMap
{
    private readonly Dictionary<string, INode> _nodes = new(StringComparer.Ordinal);

    public string DeviceName { get; internal set; } = string.Empty;
    public string ModelName { get; internal set; } = string.Empty;
    public string VendorName { get; internal set; } = string.Empty;
    public string StandardNameSpace { get; internal set; } = string.Empty;
    public Version SchemaVersion { get; internal set; } = new(1, 0, 0);

    internal IPort? Port { get; set; }

    public INode? GetNode(string name)
        => _nodes.TryGetValue(name, out var node) ? node : null;

    public IReadOnlyList<INode> Nodes => _nodes.Values.ToList().AsReadOnly();

    public void Connect(IPort port)
    {
        Port = port;

        // Wire port to all register nodes
        foreach (var node in _nodes.Values.OfType<RegisterNode>())
        {
            node.Port = port;
        }

        // Wire port to IntegerNode/FloatNode that have register addresses (IntReg/FloatReg)
        foreach (var node in _nodes.Values.OfType<IntegerNode>())
        {
            if (node.RegisterAddress.HasValue)
                node.Port = port;
        }
        foreach (var node in _nodes.Values.OfType<FloatNode>())
        {
            if (node.RegisterAddress.HasValue)
                node.Port = port;
        }

        // Wire node map to command nodes
        foreach (var node in _nodes.Values.OfType<CommandNode>())
        {
            node.NodeMap = this;
        }
    }

    public void Poll()
    {
        foreach (var node in _nodes.Values)
        {
            node.InvalidateNode();
        }
    }

    internal void AddNode(INode node)
    {
        _nodes[node.Name] = node;
    }

    /// <summary>
    /// Resolve cross-references between nodes (category children, invalidators, etc.).
    /// Called after all nodes have been added.
    /// </summary>
    internal void ResolveReferences()
    {
        foreach (var node in _nodes.Values)
        {
            if (node is CategoryNode category)
            {
                foreach (var featureName in category.FeatureNames)
                {
                    if (_nodes.TryGetValue(featureName, out var child))
                    {
                        category.AddFeature(child);
                    }
                }
            }

            // Resolve pValue references for value nodes
            if (node is IntegerNode intNode && intNode.PValueNodeName is not null)
            {
                if (_nodes.TryGetValue(intNode.PValueNodeName, out var target))
                    intNode.PValueNode = target;
            }
            else if (node is FloatNode floatNode && floatNode.PValueNodeName is not null)
            {
                if (_nodes.TryGetValue(floatNode.PValueNodeName, out var target))
                    floatNode.PValueNode = target;
            }
            else if (node is EnumerationNode enumNode && enumNode.PValueNodeName is not null)
            {
                if (_nodes.TryGetValue(enumNode.PValueNodeName, out var target))
                    enumNode.PValueNode = target;
            }
        }
    }
}
