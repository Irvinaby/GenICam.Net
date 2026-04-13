using System.Globalization;
using System.Xml.Linq;

namespace GenICam.Net.GenApi;

/// <summary>
/// Parses a GenICam camera description XML file into a <see cref="NodeMap"/>.
/// Supports GenApi schema versions 1.0 and 1.1.
/// </summary>
/// <remarks>
/// <para>This is the primary entry point for loading camera descriptions. GenICam-compliant cameras
/// provide an XML file describing all features, registers, and their relationships.</para>
/// <para><b>Supported node types:</b> Integer, IntReg, IntSwissKnife, MaskedIntReg, IntConverter,
/// Float, FloatReg, SwissKnife, Converter, Boolean, String, StringReg, Enumeration,
/// Command, Register, Category, Port.</para>
/// <para><b>Example:</b></para>
/// <code>
/// // From a file
/// var nodeMap = NodeMapParser.ParseFile("camera.xml");
///
/// // From a string
/// var nodeMap = NodeMapParser.Parse(xmlContent);
///
/// // From a stream (e.g., downloaded from camera)
/// var nodeMap = NodeMapParser.Parse(stream);
///
/// // Then connect to hardware
/// nodeMap.Connect(myTransportPort);
/// </code>
/// </remarks>
public static class NodeMapParser
{
    /// <summary>
    /// Parses a GenICam camera description XML from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">A readable stream containing the XML content.</param>
    /// <returns>A fully-resolved <see cref="NodeMap"/> with all cross-references linked.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the XML has no root element.</exception>
    public static NodeMap Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        return ParseDocument(doc);
    }

    /// <summary>
    /// Parses a GenICam camera description XML from a string.
    /// </summary>
    /// <param name="xml">The XML content as a string.</param>
    /// <returns>A fully-resolved <see cref="NodeMap"/> with all cross-references linked.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the XML has no root element.</exception>
    public static NodeMap Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        return ParseDocument(doc);
    }

    /// <summary>
    /// Parses a GenICam camera description XML from a file on disk.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the XML file.</param>
    /// <returns>A fully-resolved <see cref="NodeMap"/> with all cross-references linked.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the XML has no root element.</exception>
    public static NodeMap ParseFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Parse(stream);
    }

    private static NodeMap ParseDocument(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");

        // Strip namespace for easier element access
        var ns = root.Name.Namespace;

        var nodeMap = new NodeMap();

        // Parse root attributes
        var schemaVersionAttr = root.Attribute("SchemaMajorVersion")?.Value;
        if (schemaVersionAttr != null)
        {
            var major = int.Parse(root.Attribute("SchemaMajorVersion")?.Value ?? "1", CultureInfo.InvariantCulture);
            var minor = int.Parse(root.Attribute("SchemaMinorVersion")?.Value ?? "0", CultureInfo.InvariantCulture);
            var sub = int.Parse(root.Attribute("SchemaSubMinorVersion")?.Value ?? "0", CultureInfo.InvariantCulture);
            nodeMap.SchemaVersion = new Version(major, minor, sub);
        }

        nodeMap.ModelName = root.Attribute("ModelName")?.Value ?? string.Empty;
        nodeMap.VendorName = root.Attribute("VendorName")?.Value ?? string.Empty;
        nodeMap.StandardNameSpace = root.Attribute("StandardNameSpace")?.Value ?? string.Empty;
        nodeMap.DeviceName = root.Attribute("ProductGuid")?.Value
                          ?? root.Attribute("ModelName")?.Value
                          ?? string.Empty;

        // Parse all node elements
        foreach (var element in root.Elements())
        {
            var node = ParseNode(element, ns);
            if (node != null)
            {
                nodeMap.AddNode(node);
            }
        }

        // Resolve cross-references
        nodeMap.ResolveReferences();

        return nodeMap;
    }

    private static INode? ParseNode(XElement element, XNamespace ns)
    {
        var localName = element.Name.LocalName;

        return localName switch
        {
            "Integer" or "IntReg" or "IntSwissKnife" or "MaskedIntReg" => ParseInteger(element, ns),
            "Float" or "FloatReg" or "SwissKnife" or "Converter" => ParseFloat(element, ns),
            "Boolean" => ParseBoolean(element, ns),
            "StringReg" or "String" => ParseString(element, ns),
            "Enumeration" => ParseEnumeration(element, ns),
            "Command" => ParseCommand(element, ns),
            "Register" => ParseRegister(element, ns),
            "Category" => ParseCategory(element, ns),
            "IntConverter" => ParseInteger(element, ns),
            "Port" => ParsePort(element, ns),
            "Group" => null, // Group is a structural element, not a node
            _ => null,       // Unknown node type
        };
    }

    private static void ParseCommonAttributes(XElement element, XNamespace ns, NodeBase node)
    {
        node.Name = element.Attribute("Name")?.Value ?? element.Element(ns + "Name")?.Value ?? string.Empty;

        node.DisplayName = element.Element(ns + "DisplayName")?.Value ?? node.Name;
        node.ToolTip = element.Element(ns + "ToolTip")?.Value ?? string.Empty;
        node.Description = element.Element(ns + "Description")?.Value ?? string.Empty;

        var visibility = element.Element(ns + "Visibility")?.Value;
        if (visibility != null && Enum.TryParse<Visibility>(visibility, true, out var v))
            node.Visibility = v;

        var accessMode = element.Element(ns + "AccessMode")?.Value;
        if (accessMode != null && Enum.TryParse<AccessMode>(accessMode, true, out var am))
            node.AccessMode = am;

        // ImposedAccessMode overrides AccessMode
        var imposed = element.Element(ns + "ImposedAccessMode")?.Value;
        if (imposed != null && Enum.TryParse<AccessMode>(imposed, true, out var iam))
            node.AccessMode = iam;

        var nameSpace = element.Element(ns + "NameSpace")?.Value;
        if (nameSpace != null && Enum.TryParse<NameSpace>(nameSpace, true, out var nsVal))
            node.NameSpace = nsVal;

        var isDeprecated = element.Element(ns + "IsDeprecated")?.Value;
        if (isDeprecated != null)
            node.IsDeprecated = isDeprecated is "Yes" or "true" or "1";

        node.EventId = element.Element(ns + "EventID")?.Value ?? string.Empty;
    }

    private static IntegerNode ParseInteger(XElement element, XNamespace ns)
    {
        var node = new IntegerNode();
        ParseCommonAttributes(element, ns, node);

        var value = element.Element(ns + "Value")?.Value;
        if (value != null)
            node.SetValueDirect(ParseLong(value));

        var min = element.Element(ns + "Min")?.Value;
        if (min != null) node.Min = ParseLong(min);

        var max = element.Element(ns + "Max")?.Value;
        if (max != null) node.Max = ParseLong(max);

        var inc = element.Element(ns + "Inc")?.Value;
        if (inc != null) node.Increment = ParseLong(inc);

        var repr = element.Element(ns + "Representation")?.Value;
        if (repr != null && Enum.TryParse<Representation>(repr, true, out var r))
            node.Representation = r;

        node.Unit = element.Element(ns + "Unit")?.Value ?? string.Empty;

        // pValue: reference to another node providing the value
        node.PValueNodeName = element.Element(ns + "pValue")?.Value;

        // IntReg/MaskedIntReg specific: register address and length
        var address = element.Element(ns + "Address")?.Value;
        if (address != null)
            node.RegisterAddress = ParseLong(address);

        var pAddress = element.Element(ns + "pAddress")?.Value;
        if (pAddress != null && node.RegisterAddress is null)
            node.RegisterAddress = 0; // Will be resolved later if needed

        var length = element.Element(ns + "Length")?.Value;
        if (length != null)
            node.RegisterLength = ParseLong(length);

        var endianness = element.Element(ns + "Endianess")?.Value
                      ?? element.Element(ns + "Endianness")?.Value;
        if (endianness != null && Enum.TryParse<Endianness>(endianness, true, out var end))
            node.Endianness = end;

        // SwissKnife formula
        var formula = element.Element(ns + "Formula")?.Value;
        if (formula != null) node.Formula = formula;

        // Parse formula variables (pVariable elements)
        foreach (var pVar in element.Elements(ns + "pVariable"))
        {
            var varName = pVar.Attribute("Name")?.Value;
            var nodeName = pVar.Value;
            if (varName != null)
                node.FormulaVariables[varName] = nodeName;
        }

        return node;
    }

    private static FloatNode ParseFloat(XElement element, XNamespace ns)
    {
        var node = new FloatNode();
        ParseCommonAttributes(element, ns, node);

        var value = element.Element(ns + "Value")?.Value;
        if (value != null)
            node.SetValueDirect(double.Parse(value, CultureInfo.InvariantCulture));

        var min = element.Element(ns + "Min")?.Value;
        if (min != null) node.Min = double.Parse(min, CultureInfo.InvariantCulture);

        var max = element.Element(ns + "Max")?.Value;
        if (max != null) node.Max = double.Parse(max, CultureInfo.InvariantCulture);

        var inc = element.Element(ns + "Inc")?.Value;
        if (inc != null)
        {
            node.Increment = double.Parse(inc, CultureInfo.InvariantCulture);
            node.HasIncrement = true;
        }

        var repr = element.Element(ns + "Representation")?.Value;
        if (repr != null && Enum.TryParse<Representation>(repr, true, out var r))
            node.Representation = r;

        node.Unit = element.Element(ns + "Unit")?.Value ?? string.Empty;

        // pValue: reference to another node providing the value
        node.PValueNodeName = element.Element(ns + "pValue")?.Value;

        // FloatReg specific: register address and length
        var fAddress = element.Element(ns + "Address")?.Value;
        if (fAddress != null)
            node.RegisterAddress = ParseLong(fAddress);

        var fLength = element.Element(ns + "Length")?.Value;
        if (fLength != null)
            node.RegisterLength = ParseLong(fLength);

        var fEndianness = element.Element(ns + "Endianess")?.Value
                       ?? element.Element(ns + "Endianness")?.Value;
        if (fEndianness != null && Enum.TryParse<Endianness>(fEndianness, true, out var fEnd))
            node.Endianness = fEnd;

        var formula = element.Element(ns + "Formula")?.Value;
        if (formula != null) node.Formula = formula;

        foreach (var pVar in element.Elements(ns + "pVariable"))
        {
            var varName = pVar.Attribute("Name")?.Value;
            var nodeName = pVar.Value;
            if (varName != null)
                node.FormulaVariables[varName] = nodeName;
        }

        return node;
    }

    private static BooleanNode ParseBoolean(XElement element, XNamespace ns)
    {
        var node = new BooleanNode();
        ParseCommonAttributes(element, ns, node);

        var value = element.Element(ns + "Value")?.Value;
        if (value != null)
            node.SetValueDirect(value is "1" or "true" or "True");

        return node;
    }

    private static StringNode ParseString(XElement element, XNamespace ns)
    {
        var node = new StringNode();
        ParseCommonAttributes(element, ns, node);

        var value = element.Element(ns + "Value")?.Value;
        if (value != null)
            node.SetValueDirect(value);

        var maxLen = element.Element(ns + "MaxLength")?.Value;
        if (maxLen != null) node.MaxLength = ParseLong(maxLen);

        return node;
    }

    private static EnumerationNode ParseEnumeration(XElement element, XNamespace ns)
    {
        var node = new EnumerationNode();
        ParseCommonAttributes(element, ns, node);

        foreach (var entryElem in element.Elements(ns + "EnumEntry"))
        {
            var entry = new EnumEntryNode();
            ParseCommonAttributes(entryElem, ns, entry);

            entry.Symbolic = entry.Name;

            var numVal = entryElem.Element(ns + "Value")?.Value;
            if (numVal != null)
                entry.NumericValue = ParseLong(numVal);

            var isSelfClearing = entryElem.Element(ns + "IsSelfClearing")?.Value;
            if (isSelfClearing != null)
                entry.IsSelfClearing = isSelfClearing is "Yes" or "true" or "1";

            node.AddEntry(entry);
        }

        var value = element.Element(ns + "Value")?.Value;
        if (value != null)
            node.SetValueDirect(value);

        // pValue: reference to a register providing the numeric value
        node.PValueNodeName = element.Element(ns + "pValue")?.Value;

        return node;
    }

    private static CommandNode ParseCommand(XElement element, XNamespace ns)
    {
        var node = new CommandNode();
        ParseCommonAttributes(element, ns, node);

        var cmdValue = element.Element(ns + "CommandValue")?.Value;
        if (cmdValue != null)
            node.CommandValue = ParseLong(cmdValue);

        node.RegisterNodeName = element.Element(ns + "pValue")?.Value
                             ?? element.Element(ns + "pCommandRegister")?.Value;

        return node;
    }

    private static RegisterNode ParseRegister(XElement element, XNamespace ns)
    {
        var node = new RegisterNode();
        ParseCommonAttributes(element, ns, node);

        var address = element.Element(ns + "Address")?.Value;
        if (address != null) node.Address = ParseLong(address);

        var length = element.Element(ns + "Length")?.Value;
        if (length != null) node.Length = ParseLong(length);

        var endianness = element.Element(ns + "Endianess")?.Value  // Note: GenICam XML uses "Endianess" (sic)
                      ?? element.Element(ns + "Endianness")?.Value;
        if (endianness != null && Enum.TryParse<Endianness>(endianness, true, out var e))
            node.Endianness = e;

        return node;
    }

    private static CategoryNode ParseCategory(XElement element, XNamespace ns)
    {
        var node = new CategoryNode();
        ParseCommonAttributes(element, ns, node);

        // Collect references to child features (pFeature elements)
        foreach (var pFeature in element.Elements(ns + "pFeature"))
        {
            node.FeatureNames.Add(pFeature.Value);
        }

        return node;
    }

    private static RegisterNode ParsePort(XElement element, XNamespace ns)
    {
        // Port is modeled as a special register node
        var node = new RegisterNode();
        ParseCommonAttributes(element, ns, node);
        return node;
    }

    private static long ParseLong(string value)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            return long.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return long.Parse(value, CultureInfo.InvariantCulture);
    }
}
