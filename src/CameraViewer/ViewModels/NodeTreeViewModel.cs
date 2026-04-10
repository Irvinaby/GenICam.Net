using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GenICam.Net.GenApi;

namespace CameraViewer.ViewModels;

/// <summary>
/// Builds and exposes the hierarchical node tree from an <see cref="INodeMap"/>,
/// with a visibility filter (Beginner / Expert / Guru).
/// </summary>
public sealed partial class NodeTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private Visibility _visibilityFilter = Visibility.Beginner;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<NodeViewModel> Categories { get; } = [];

    /// <summary>Loads the node tree from the given map.</summary>
    public void Load(INodeMap nodeMap)
    {
        Categories.Clear();

        var categories = nodeMap.Nodes
            .OfType<ICategory>()
            .Select(c => new NodeViewModel(c))
            .ToList();

        // If no categories, show all non-category nodes flat
        if (categories.Count == 0)
        {
            foreach (var vm in nodeMap.Nodes
                .Where(n => n is not ICategory)
                .Select(n => new NodeViewModel(n)))
                Categories.Add(vm);
        }
        else
        {
            foreach (var vm in categories)
                Categories.Add(vm);
        }

        ApplyFilter();
    }

    partial void OnVisibilityFilterChanged(Visibility value) => ApplyFilter();
    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        // Visibility and text filter are applied in the View via CollectionViewSource / DataTemplate triggers.
        // Raising PropertyChanged on Categories causes the View to re-evaluate.
        OnPropertyChanged(nameof(Categories));
    }

    /// <summary>
    /// Returns true if the given node should be visible given current filter settings.
    /// Used by the View's DataTemplate selectors.
    /// </summary>
    public bool IsVisible(NodeViewModel node) =>
        node.NodeVisibility <= VisibilityFilter &&
        (string.IsNullOrWhiteSpace(FilterText) ||
         node.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
         node.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
}
