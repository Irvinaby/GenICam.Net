using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GenApi;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Wraps a single <see cref="INode"/> and exposes typed value properties suitable for WPF binding.
/// Supports IInteger, IFloat, IBoolean, IString, IEnumeration, ICommand, and ICategory.
/// </summary>
public sealed partial class NodeViewModel : ObservableObject
{
    private readonly INode _node;
    private readonly ILogger<NodeViewModel>? _logger;
    private readonly Action<string, bool>? _reportStatus;

    public NodeViewModel(INode node) : this(node, null) { }

    public NodeViewModel(INode node, ILogger<NodeViewModel>? logger, Action<string, bool>? reportStatus)
        : this(node, logger, reportStatus, null)
    {
    }

    private NodeViewModel(INode node, HashSet<string>? visited)
        : this(node, null, null, visited)
    {
    }

    private NodeViewModel(INode node, ILogger<NodeViewModel>? logger, Action<string, bool>? reportStatus, HashSet<string>? visited)
    {
        _node = node;
        _logger = logger;
        _reportStatus = reportStatus;

        if (node is ICategory cat)
        {
            visited ??= new HashSet<string>(StringComparer.Ordinal);
            if (!visited.Add(node.Name))
            {
                // Circular reference detected — stop recursion
                Children = [];
                return;
            }
            Children = new ObservableCollection<NodeViewModel>(
                cat.Features.Select(f => new NodeViewModel(f, logger, reportStatus, new HashSet<string>(visited, StringComparer.Ordinal))));
        }
        else
        {
            Children = [];
        }
    }

    public string Name => _node.Name;
    public string DisplayName => _node.DisplayName;
    public string ToolTip => _node.ToolTip;
    public Visibility NodeVisibility => _node.Visibility;
    public AccessMode AccessMode => _node.AccessMode;
    public bool IsReadOnly => _node.AccessMode is AccessMode.RO or AccessMode.NA or AccessMode.NI;
    public bool IsWritable => !IsReadOnly;
    public bool IsReadable => _node.AccessMode is AccessMode.RO or AccessMode.RW;

    // Type discriminators
    public bool IsCategory  => _node is ICategory;
    public bool IsInteger   => _node is IInteger;
    public bool IsFloat     => _node is IFloat;
    public bool IsBoolean   => _node is IBoolean;
    public bool IsString    => _node is IString and not IEnumeration;
    public bool IsEnum      => _node is IEnumeration;
    public bool IsCommand   => _node is ICommand;

    // Integer
    public long IntegerValue
    {
        get { try { return IsReadable && _node is IInteger i ? i.Value : 0; } catch { return 0; } }
        set
        {
            if (_node is IInteger i)
                WriteNodeValue(value, () => { if (!IsReadable || i.Value != value) i.Value = value; });
        }
    }
    public long IntegerMin => _node is IInteger i ? i.Min : 0;
    public long IntegerMax => _node is IInteger i ? i.Max : 0;
    public string IntegerUnit => _node is IInteger i ? i.Unit : string.Empty;

    // Float
    public double FloatValue
    {
        get { try { return IsReadable && _node is IFloat f ? f.Value : 0.0; } catch { return 0.0; } }
        set
        {
            if (_node is IFloat f)
                WriteNodeValue(value, () => f.Value = value);
        }
    }
    public double FloatMin => _node is IFloat f ? f.Min : 0.0;
    public double FloatMax => _node is IFloat f ? f.Max : 0.0;
    public string FloatUnit => _node is IFloat f ? f.Unit : string.Empty;

    // Boolean
    public bool BoolValue
    {
        get { try { return IsReadable && _node is IBoolean b && b.Value; } catch { return false; } }
        set
        {
            if (_node is IBoolean b)
                WriteNodeValue(value, () => b.Value = value);
        }
    }

    // String
    public string StringValue
    {
        get { try { return IsReadable && _node is IString s ? s.Value : string.Empty; } catch { return string.Empty; } }
        set
        {
            if (_node is IString s)
                WriteNodeValue(value, () => { if (!IsReadable || s.Value != value) s.Value = value; });
        }
    }

    // Enumeration
    public string EnumValue
    {
        get { try { return IsReadable && _node is IEnumeration e ? e.Value : string.Empty; } catch { return string.Empty; } }
        set
        {
            if (_node is IEnumeration e)
                WriteNodeValue(value, () => { if (!IsReadable || e.Value != value) e.Value = value; });
        }
    }
    public IReadOnlyList<string> EnumEntries => _node is IEnumeration e
        ? e.Entries.Select(en => en.Symbolic).ToList()
        : [];

    // Display value for read-only presentation
    public string DisplayValue
    {
        get
        {
            try
            {
                if (!IsReadable)
                    return _node.AccessMode == AccessMode.WO ? "<write-only>" : string.Empty;

                return _node switch
                {
                    IInteger i => $"{i.Value}{(string.IsNullOrEmpty(i.Unit) ? "" : " " + i.Unit)}",
                    IFloat f   => $"{f.Value:G6}{(string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit)}",
                    IBoolean b => b.Value ? "true" : "false",
                    IString s  => s.Value,
                    IEnumeration e => e.Value,
                    ICommand   => "(command)",
                    ICategory  => string.Empty,
                    _          => string.Empty,
                };
            }
            catch
            {
                return "(error)";
            }
        }
    }

    // Children (for categories)
    public ObservableCollection<NodeViewModel> Children { get; }

    [RelayCommand]
    private void ExecuteCommand()
    {
        if (_node is ICommand cmd)
            WriteNodeValue("(command)", cmd.Execute, "Executed");
    }

    private void WriteNodeValue<T>(
        T value,
        Action write,
        string successVerb = "Wrote",
        [CallerMemberName] string? propertyName = null)
    {
        try
        {
            _logger?.LogDebug(
                "Writing node {NodeName} ({DisplayName}) value {Value}",
                Name,
                DisplayName,
                value);

            write();

            var status = $"{successVerb} {DisplayName}: {value}";
            _logger?.LogInformation(
                "Node write succeeded: {NodeName} ({DisplayName}) = {Value}",
                Name,
                DisplayName,
                value);
            _reportStatus?.Invoke(status, false);
        }
        catch (Exception ex)
        {
            var status = $"Failed to write {DisplayName}: {ex.Message}";
            _logger?.LogWarning(
                ex,
                "Node write failed: {NodeName} ({DisplayName}) = {Value}",
                Name,
                DisplayName,
                value);
            _reportStatus?.Invoke(status, true);
        }
        finally
        {
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(DisplayValue));
        }
    }
}
