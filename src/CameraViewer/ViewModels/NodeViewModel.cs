using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GenApi;

namespace CameraViewer.ViewModels;

/// <summary>
/// Wraps a single <see cref="INode"/> and exposes typed value properties suitable for WPF binding.
/// Supports IInteger, IFloat, IBoolean, IString, IEnumeration, ICommand, and ICategory.
/// </summary>
public sealed partial class NodeViewModel : ObservableObject
{
    private readonly INode _node;

    public NodeViewModel(INode node) : this(node, null) { }

    private NodeViewModel(INode node, HashSet<string>? visited)
    {
        _node = node;

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
                cat.Features.Select(f => new NodeViewModel(f, visited)));
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
        get { try { return _node is IInteger i ? i.Value : 0; } catch { return 0; } }
        set { if (_node is IInteger i && i.Value != value) { try { i.Value = value; } catch { } OnPropertyChanged(); } }
    }
    public long IntegerMin => _node is IInteger i ? i.Min : 0;
    public long IntegerMax => _node is IInteger i ? i.Max : 0;
    public string IntegerUnit => _node is IInteger i ? i.Unit : string.Empty;

    // Float
    public double FloatValue
    {
        get { try { return _node is IFloat f ? f.Value : 0.0; } catch { return 0.0; } }
        set { if (_node is IFloat f) { try { f.Value = value; } catch { } OnPropertyChanged(); } }
    }
    public double FloatMin => _node is IFloat f ? f.Min : 0.0;
    public double FloatMax => _node is IFloat f ? f.Max : 0.0;
    public string FloatUnit => _node is IFloat f ? f.Unit : string.Empty;

    // Boolean
    public bool BoolValue
    {
        get { try { return _node is IBoolean b && b.Value; } catch { return false; } }
        set { if (_node is IBoolean b) { try { b.Value = value; } catch { } OnPropertyChanged(); } }
    }

    // String
    public string StringValue
    {
        get { try { return _node is IString s ? s.Value : string.Empty; } catch { return string.Empty; } }
        set { if (_node is IString s && s.Value != value) { try { s.Value = value; } catch { } OnPropertyChanged(); } }
    }

    // Enumeration
    public string EnumValue
    {
        get { try { return _node is IEnumeration e ? e.Value : string.Empty; } catch { return string.Empty; } }
        set { if (_node is IEnumeration e && e.Value != value) { try { e.Value = value; } catch { } OnPropertyChanged(); } }
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
            cmd.Execute();
    }
}
