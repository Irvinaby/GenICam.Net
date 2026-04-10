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

    public NodeViewModel(INode node)
    {
        _node = node;
        Children = node is ICategory cat
            ? new ObservableCollection<NodeViewModel>(cat.Features.Select(f => new NodeViewModel(f)))
            : [];
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
        get => _node is IInteger i ? i.Value : 0;
        set { if (_node is IInteger i && i.Value != value) { i.Value = value; OnPropertyChanged(); } }
    }
    public long IntegerMin => _node is IInteger i ? i.Min : 0;
    public long IntegerMax => _node is IInteger i ? i.Max : 0;
    public string IntegerUnit => _node is IInteger i ? i.Unit : string.Empty;

    // Float
    public double FloatValue
    {
        get => _node is IFloat f ? f.Value : 0.0;
        set { if (_node is IFloat f && Math.Abs(f.Value - value) > double.Epsilon) { f.Value = value; OnPropertyChanged(); } }
    }
    public double FloatMin => _node is IFloat f ? f.Min : 0.0;
    public double FloatMax => _node is IFloat f ? f.Max : 0.0;
    public string FloatUnit => _node is IFloat f ? f.Unit : string.Empty;

    // Boolean
    public bool BoolValue
    {
        get => _node is IBoolean b && b.Value;
        set { if (_node is IBoolean b) { b.Value = value; OnPropertyChanged(); } }
    }

    // String
    public string StringValue
    {
        get => _node is IString s ? s.Value : string.Empty;
        set { if (_node is IString s && s.Value != value) { s.Value = value; OnPropertyChanged(); } }
    }

    // Enumeration
    public string EnumValue
    {
        get => _node is IEnumeration e ? e.Value : string.Empty;
        set { if (_node is IEnumeration e && e.Value != value) { e.Value = value; OnPropertyChanged(); } }
    }
    public IReadOnlyList<string> EnumEntries => _node is IEnumeration e
        ? e.Entries.Select(en => en.Symbolic).ToList()
        : [];

    // Display value for read-only presentation
    public string DisplayValue => _node switch
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

    // Children (for categories)
    public ObservableCollection<NodeViewModel> Children { get; }

    [RelayCommand]
    private void ExecuteCommand()
    {
        if (_node is ICommand cmd)
            cmd.Execute();
    }
}
