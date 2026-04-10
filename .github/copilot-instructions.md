# Copilot Instructions for GenICam.Net

## Project Overview

GenICam.Net is a .NET 8 implementation of the [GenICam](https://www.emva.org/standards-technology/genicam/) and GigE Vision standards for industrial camera integration. The library parses GenICam XML camera descriptions into a typed node tree and provides register-level device access through a pluggable transport layer.

## Architecture

- **GenApi module** (`src/GenICam.Net/GenApi/`) — the core of the library, implementing the GenICam GenApi standard:
  - `Enums/` — Standard enum types (AccessMode, Visibility, Representation, etc.)
  - `Interfaces/` — Public API contracts (INode, IValue, IInteger, IFloat, IBoolean, IString, IEnumeration, ICommand, IRegister, ICategory, IPort, INodeMap)
  - `Nodes/` — Concrete node implementations (IntegerNode, FloatNode, etc.)
  - `NodeBase.cs`, `ValueNode.cs` — Abstract base classes for the node hierarchy
  - `NodeMap.cs` — Central node registry implementing INodeMap
  - `NodeMapParser.cs` — XML parser that reads GenICam camera description files
- **Planned modules**: SFNC (Standard Feature Naming Convention), GenTL (Generic Transport Layer), GigE Vision protocol

## Build & Test

```bash
dotnet build
dotnet test
```

- Test framework: **NUnit 3** (not xUnit)
- Test project: `tests/GenICam.Net.Tests/`
- The main project exposes internals to the test project via `InternalsVisibleTo`

## Conventions

### Code Style
- File-scoped namespaces (no braces)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- All public interfaces and classes must have XML documentation comments (`///`)

### Naming
- Interfaces: `I{NodeType}` (e.g., `IInteger`, `IFloat`, `ICommand`)
- Concrete nodes: `{NodeType}Node` (e.g., `IntegerNode`, `FloatNode`, `CommandNode`)
- Enum entries follow the GenICam specification naming (e.g., `AccessMode.RW`, `Visibility.Guru`)
- Test classes: `{ClassUnderTest}Tests` (e.g., `IntegerNodeTests`, `NodeMapParserTests`)

### Folder Organization
- Enums go in `GenApi/Enums/`
- Public interfaces go in `GenApi/Interfaces/`
- Concrete node implementations go in `GenApi/Nodes/`
- Infrastructure (base classes, parser, node map) stays in `GenApi/` root

### Node Implementation Pattern
- All nodes inherit `NodeBase` (metadata) or `ValueNode` (metadata + value + change events)
- Properties settable by the parser use `internal set`
- Provide a `SetValueDirect()` internal method for parser/test use that bypasses validation
- Value setters validate range/constraints and call `OnValueChanged()`

### Transport Layer
- `IPort` is the bridge between the node tree and physical hardware
- Implement `IPort.Read`/`Write` for a specific transport (GigE Vision, USB3 Vision, etc.)
- Call `NodeMap.Connect(port)` to wire register and command nodes to the port

## Standards Reference
- GenICam GenApi: Camera description XML schema, node types, register model
- GenICam SFNC: Standard feature names (Width, Height, PixelFormat, ExposureTime, etc.)
- GenICam GenTL: Transport layer abstraction for device enumeration and image acquisition
- GigE Vision: UDP-based protocol for camera discovery (GVCP) and image streaming (GVSP)
- See https://www.emva.org/standards-technology/genicam/ for specification documents
