# GenICam.Net

A .NET implementation of the [GenICam](https://www.emva.org/standards-technology/genicam/) and GigE Vision standards.

## Overview

GenICam.Net provides a managed .NET library for working with GenICam-compliant industrial cameras. The project implements:

- **GenApi** – Camera description XML parser and node map (Integer, Float, Boolean, String, Enumeration, Command, Register, Category nodes)
- **SFNC** – Standard Feature Naming Convention support (planned)
- **GenTL** – Generic Transport Layer interface (planned)
- **GigE Vision** – GigE Vision protocol implementation (planned)

## Project Structure

```
src/
  GenICam.Net/                  # Core library
    GenApi/
      Enums/                    # AccessMode, Visibility, CachingMode, NameSpace,
                                # Representation, Endianness, Sign, Slope
      Interfaces/               # INode, IValue, IInteger, IFloat, IBoolean, IString,
                                # IEnumeration, IEnumEntry, ICommand, IRegister,
                                # ICategory, IPort, INodeMap
      Nodes/                    # IntegerNode, FloatNode, BooleanNode, StringNode,
                                # EnumerationNode, EnumEntryNode, CommandNode,
                                # RegisterNode, CategoryNode
      NodeBase.cs               # Abstract base for all nodes
      ValueNode.cs              # Abstract base for value-carrying nodes
      NodeMap.cs                # INodeMap implementation
      NodeMapParser.cs          # XML camera description parser
tests/
  GenICam.Net.Tests/            # NUnit tests
```

## Usage

### 1. Parse a Camera Description XML

Every GenICam-compliant camera provides an XML file describing its features and register layout. Parse it to get a `NodeMap`:

```csharp
using GenICam.Net.GenApi;

// From a file
var nodeMap = NodeMapParser.ParseFile("camera.xml");

// From a string
var nodeMap = NodeMapParser.Parse(xmlContent);

// From a stream (e.g., read from camera memory)
var nodeMap = NodeMapParser.Parse(stream);

// Inspect device metadata
Console.WriteLine($"Camera: {nodeMap.VendorName} {nodeMap.ModelName}");
Console.WriteLine($"Schema: {nodeMap.SchemaVersion}");
```

### 2. Connect to a Transport Layer

To read/write actual hardware registers, implement `IPort` for your transport (GigE Vision, USB3 Vision, etc.) and connect it:

```csharp
public class GigEPort : IPort
{
    public byte[] Read(long address, long length)
    {
        // Send GVCP ReadMem command over UDP
    }

    public void Write(long address, byte[] data)
    {
        // Send GVCP WriteMem command over UDP
    }
}

nodeMap.Connect(new GigEPort(cameraIpAddress));
```

### 3. Read and Write Integer Features

```csharp
var width = (IInteger)nodeMap.GetNode("Width")!;

// Read current value and range
Console.WriteLine($"Width = {width.Value} {width.Unit}");
Console.WriteLine($"Range: [{width.Min}, {width.Max}], step {width.Increment}");

// Set a new value (validates range and increment)
width.Value = 1920;
```

### 4. Read and Write Float Features

```csharp
var exposure = (IFloat)nodeMap.GetNode("ExposureTime")!;

Console.WriteLine($"Exposure = {exposure.Value} {exposure.Unit}");
Console.WriteLine($"Range: [{exposure.Min}, {exposure.Max}]");

exposure.Value = 15000.0; // 15 ms
```

### 5. Read and Write Boolean Features

```csharp
var reverseX = (IBoolean)nodeMap.GetNode("ReverseX")!;
Console.WriteLine($"ReverseX = {reverseX.Value}");
reverseX.Value = true;
```

### 6. Work with Enumerations

```csharp
var pixelFormat = (IEnumeration)nodeMap.GetNode("PixelFormat")!;

// List available entries
foreach (var entry in pixelFormat.Entries)
    Console.WriteLine($"  {entry.Symbolic} = {entry.NumericValue}");

// Set by symbolic name
pixelFormat.Value = "Mono8";

// Or set by numeric value
pixelFormat.IntValue = 2;

// Look up a specific entry
var entry = pixelFormat.GetEntryByName("RGB8");
```

### 7. Read and Write String Features

```csharp
var userId = (IString)nodeMap.GetNode("DeviceUserID")!;
Console.WriteLine($"User ID: {userId.Value} (max {userId.MaxLength} chars)");
userId.Value = "MyCamera01";
```

### 8. Execute Commands

```csharp
var startCmd = (ICommand)nodeMap.GetNode("AcquisitionStart")!;

// Check availability
if (startCmd.AccessMode != AccessMode.NA)
{
    startCmd.Execute();
    
    // Poll for completion (for async commands)
    while (!startCmd.IsDone)
        Thread.Sleep(10);
}
```

### 9. Raw Register Access

```csharp
var register = (IRegister)nodeMap.GetNode("SensorRegister")!;

Console.WriteLine($"Address: 0x{register.Address:X}, Length: {register.Length}");

// Read raw bytes
byte[] data = register.Get(register.Length);

// Write raw bytes
register.Set(new byte[] { 0x01, 0x02, 0x03, 0x04 });
```

### 10. Browse the Feature Tree

```csharp
var category = (ICategory)nodeMap.GetNode("ImageFormatControl")!;

Console.WriteLine($"Category: {category.DisplayName}");
foreach (var feature in category.Features)
{
    Console.WriteLine($"  {feature.Name} ({feature.GetType().Name}): {feature.DisplayName}");
    Console.WriteLine($"    Visibility: {feature.Visibility}, Access: {feature.AccessMode}");
}
```

### 11. Subscribe to Value Changes

```csharp
var width = (IValue)nodeMap.GetNode("Width")!;
width.ValueChanged += (sender, args) =>
{
    Console.WriteLine($"Width changed to: {width.ValueAsString}");
};
```

### 12. Type-Agnostic Value Access

All value nodes support string-based access via `IValue`:

```csharp
// Read any value as a string
var node = (IValue)nodeMap.GetNode("Width")!;
Console.WriteLine(node.ValueAsString); // "1920"

// Write any value from a string
node.ValueAsString = "1280";
```

### 13. Invalidate and Refresh

```csharp
// Invalidate all cached values (re-read from device on next access)
nodeMap.Poll();

// Invalidate a single node
nodeMap.GetNode("Width")!.InvalidateNode();
```

## Building

```bash
dotnet build
dotnet test
```

## License

Apache License 2.0 – see [LICENSE](LICENSE) for details.
