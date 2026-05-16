# GenICam.Net

A managed .NET implementation of [GenICam](https://www.emva.org/standards-technology/genicam/) GenApi and GigE Vision components for industrial camera integration.

## Overview

GenICam.Net is split into reusable protocol libraries plus a WPF camera viewer sample application:

- **GenICam.Net** - Core GenApi support, including camera XML parsing, node maps, node interfaces, value conversion, register-backed nodes, commands, categories, and formula evaluation.
- **GigEVision.Net** - GigE Vision support for GVCP camera discovery/control, GenICam XML loading, register and memory access, GVSP packet/frame receiving, stream sessions, and display-buffer conversion.
- **CameraViewer** - A WPF viewer that discovers GigE Vision cameras, connects to a camera node map, browses feature nodes, starts/stops acquisition, and renders GVSP frames.

The viewer uses Autofac as its dependency injection container. Protocol-level services expose interfaces so they can be injected, replaced in tests, or adapted by applications that host the libraries.

## Project Structure

```text
src/
  GenICam.Net/
    GenApi/
      Enums/                    # AccessMode, Visibility, CachingMode, NameSpace,
                                # Representation, Endianness, Sign, Slope
      Interfaces/               # INode, IValue, IInteger, IFloat, IBoolean, IString,
                                # IEnumeration, IEnumEntry, ICommand, IRegister,
                                # ICategory, IPort, INodeMap
      Nodes/                    # IntegerNode, FloatNode, BooleanNode, StringNode,
                                # EnumerationNode, EnumEntryNode, CommandNode,
                                # RegisterNode, CategoryNode
      FormulaEvaluator.cs       # GenApi formula evaluation support
      NodeMap.cs                # INodeMap implementation
      NodeMapParser.cs          # XML camera description parser

  GigEVision.Net/
    GigEVision/
      Gvcp/                     # Discovery, GVCP client, XML loader, GigE port,
                                # camera session, injectable service interfaces
      Gvsp/                     # GVSP packets, receiver, stream session,
                                # frame/display conversion interfaces

  CameraViewer/
    DependencyInjection/        # Autofac composition root
    ViewModels/                 # MVVM state and camera/stream coordination
    Views/                      # WPF views
    Themes/                     # WPF styles

tests/
  GenICam.Net.Tests/
  GigEVision.Net.Tests/
```

## Dependency Injection

`CameraViewer` builds its Autofac container at startup in `App.xaml.cs` and registers application services through `CameraViewer.DependencyInjection.CameraViewerModule`.

Registered services include:

- `IGigECameraDiscoveryService` -> `GigECameraDiscoveryService`
- `IGigECameraSessionFactory` -> `GigECameraSessionFactory`
- `IGvspStreamSession` -> `GvspStreamSession`
- `IGvspDisplayConverter` -> `GvspDisplayConverter`
- `CameraViewModel`, `NodeTreeViewModel`, `StreamViewModel`, `MainViewModel`
- `ILogger<T>` backed by the application `ILoggerFactory`

This keeps the WPF composition root in the app project while the protocol libraries remain usable without Autofac.

## Usage

### Reference the Libraries

For local development, reference the projects you need:

```bash
dotnet add <your-app>.csproj reference src/GenICam.Net/GenICam.Net.csproj
dotnet add <your-app>.csproj reference src/GigEVision.Net/GigEVision.Net.csproj
```

Use `GenICam.Net` by itself when you already have a camera XML file or your own transport implementation. Add `GigEVision.Net` when you want GigE Vision discovery, GVCP register/memory access, XML loading from the camera, or GVSP image streaming.

### Choose the Right Entry Point

There are three common ways to use the libraries:

- **Offline XML parsing:** parse a GenICam XML file with `NodeMapParser`, inspect nodes, and validate feature metadata without camera hardware.
- **Custom transport:** parse XML with `NodeMapParser`, implement or provide an `IPort`, then call `nodeMap.Connect(port)` so register-backed nodes can read/write hardware.
- **GigE Vision camera workflow:** use `IGigECameraDiscoveryService` and `IGigECameraSessionFactory` from `GigEVision.Net`; the session loads XML, connects the node map to GVCP, and exposes acquisition helpers.

### Parse a Camera Description XML

Every GenICam-compliant camera provides an XML file describing its features and register layout. Parse it to get a `NodeMap`:

```csharp
using GenICam.Net.GenApi;

var nodeMap = NodeMapParser.ParseFile("camera.xml");

Console.WriteLine($"Camera: {nodeMap.VendorName} {nodeMap.ModelName}");
Console.WriteLine($"Schema: {nodeMap.SchemaVersion}");
```

You can also parse from a string or stream:

```csharp
var fromString = NodeMapParser.Parse(xmlContent);
var fromStream = NodeMapParser.Parse(stream);
```

### Connect a Node Map to a Transport

To read and write actual hardware registers, connect the node map to an `IPort`. `GigEVision.Net` provides `GigEPort` for GVCP-backed access.

```csharp
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using System.Net;

var cameraEndPoint = new IPEndPoint(cameraIpAddress, GvcpConstants.Port);
using var transport = new UdpTransportAdapter();
using var client = new GvcpClient(transport, cameraEndPoint);

var nodeMap = NodeMapParser.ParseFile("camera.xml");
nodeMap.Connect(new GigEPort(client));
```

If you are connecting to a normal GigE Vision camera, prefer `GigECameraSessionFactory` unless you specifically need this lower-level control path.

### Discover GigE Vision Cameras

```csharp
using GenICam.Net.GigEVision.Gvcp;

IGigECameraDiscoveryService discovery = new GigECameraDiscoveryService();
var cameras = await discovery.DiscoverAsync(timeoutMs: 3000);

foreach (var camera in cameras)
    Console.WriteLine($"{camera.ManufacturerName} {camera.ModelName} at {camera.IpAddress}");
```

Discovery uses UDP broadcast. Make sure the camera and host are on a reachable network interface and that local firewall rules allow GigE Vision traffic.

### Connect to a GigE Vision Camera

`GigECameraSessionFactory` creates a connected session, loads the camera XML, connects the resulting node map to a `GigEPort`, and prepares common acquisition defaults.

```csharp
using GenICam.Net.GigEVision.Gvcp;

IGigECameraSessionFactory sessions = new GigECameraSessionFactory();
using var session = await sessions.ConnectAsync(cameras[0]);

var nodeMap = session.NodeMap;
Console.WriteLine($"Connected to {session.Camera.ModelName}");
```

The session owns the GVCP client and should be disposed when you are done. Its `NodeMap` is connected to the camera, so feature reads and writes go through GVCP.

### Read and Write Features

```csharp
using GenICam.Net.GenApi;

var width = (IInteger)nodeMap.GetNode("Width")!;
Console.WriteLine($"Width = {width.Value} {width.Unit}");
Console.WriteLine($"Range: [{width.Min}, {width.Max}], step {width.Increment}");

if (width.AccessMode is AccessMode.RW or AccessMode.WO)
    width.Value = 1920;

var exposure = (IFloat)nodeMap.GetNode("ExposureTime")!;
if (exposure.AccessMode is AccessMode.RW or AccessMode.WO)
    exposure.Value = 15000.0;

var reverseX = (IBoolean)nodeMap.GetNode("ReverseX")!;
if (reverseX.AccessMode is AccessMode.RW or AccessMode.WO)
    reverseX.Value = true;
```

Use `AccessMode` before writing features because cameras may expose nodes as read-only, write-only, unavailable, or dependent on another feature state.

### Work with Enumerations

```csharp
var pixelFormat = (IEnumeration)nodeMap.GetNode("PixelFormat")!;

foreach (var entry in pixelFormat.Entries)
    Console.WriteLine($"{entry.Symbolic} = {entry.NumericValue}");

if (pixelFormat.GetEntryByName("Mono8") is not null &&
    pixelFormat.AccessMode is AccessMode.RW or AccessMode.WO)
{
    pixelFormat.Value = "Mono8";
}
```

### Execute Commands

```csharp
var start = (ICommand)nodeMap.GetNode("AcquisitionStart")!;

if (start.AccessMode != AccessMode.NA)
{
    start.Execute();

    while (!start.IsDone)
        Thread.Sleep(10);
}
```

### Stream Frames

For full camera acquisition, use `IGigECameraSession` together with `IGvspStreamSession`:

```csharp
using GenICam.Net.GigEVision.Gvsp;

using IGvspStreamSession stream = new GvspStreamSession();
stream.FrameReceived += (_, frame) =>
{
    Console.WriteLine($"Frame {frame.FrameId}: {frame.SizeX}x{frame.SizeY}");
};

var localPort = stream.Start(50000);
await session.StartAcquisitionAsync(localPort);

// Later:
await session.StopAcquisitionAsync();
stream.Stop();
```

Use `IGvspDisplayConverter` when you need UI-ready display buffers:

```csharp
IGvspDisplayConverter converter = new GvspDisplayConverter();

if (converter.TryConvert(frame, out var displayFrame))
    Console.WriteLine($"{displayFrame.Width}x{displayFrame.Height} {displayFrame.FormatName}");
```

`GvspStreamSession` receives frames on a local UDP port. `StartAcquisitionAsync(localPort)` tells the camera to stream to that port. Stop acquisition before disposing the stream session so the camera does not continue sending packets.

### Browse the Feature Tree

```csharp
var category = (ICategory)nodeMap.GetNode("ImageFormatControl")!;

foreach (var feature in category.Features)
{
    Console.WriteLine($"{feature.Name}: {feature.DisplayName}");
    Console.WriteLine($"Visibility: {feature.Visibility}, Access: {feature.AccessMode}");
}
```

### Type-Agnostic Value Access

All value nodes support string-based access through `IValue`:

```csharp
var value = (IValue)nodeMap.GetNode("Width")!;
Console.WriteLine(value.ValueAsString);
value.ValueAsString = "1280";
```

### Refresh Cached Values

```csharp
nodeMap.Poll();
nodeMap.GetNode("Width")!.InvalidateNode();
```

### Register Services in Your Own App

The protocol libraries do not require Autofac, but the service interfaces are easy to register in any host. With Autofac:

```csharp
using Autofac;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Information));
var builder = new ContainerBuilder();

builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();
builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));
builder.RegisterType<GigECameraDiscoveryService>().As<IGigECameraDiscoveryService>();
builder.RegisterType<GigECameraSessionFactory>().As<IGigECameraSessionFactory>();
builder.RegisterType<GvspStreamSession>().As<IGvspStreamSession>();
builder.RegisterType<GvspDisplayConverter>().As<IGvspDisplayConverter>();

using var container = builder.Build();
var discovery = container.Resolve<IGigECameraDiscoveryService>();
```

`CameraViewer` has a complete example in `CameraViewer.DependencyInjection.CameraViewerModule`.

## CameraViewer

`CameraViewer` is a WPF app for GigE Vision cameras. It provides:

- Camera discovery over GVCP.
- Camera connection and GenICam XML loading.
- A browsable GenApi node tree with visibility filtering.
- Feature read/write support through the node view-models.
- GVSP streaming with packet statistics and display conversion.
- Serilog file logging under the app output directory.

Run it with:

```bash
dotnet run --project src/CameraViewer/CameraViewer.csproj
```

## Building and Testing

Build the full solution:

```bash
dotnet build GenICam.Net.sln
```

Run the tests:

```bash
dotnet test GenICam.Net.sln
```

The core libraries target .NET 8 and .NET 9. `CameraViewer` targets `net8.0-windows` and requires Windows/WPF.

## License

Apache License 2.0 - see [LICENSE](LICENSE) for details.
