using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyteQuery.Features.ObjectExplorer.Models;

public enum DbNodeType
{
    Root,
    Folder,
    SchemaFolder,
    Table,
    View,
    StoredProcedure,
    Function,
    Column,
    Trigger
}

/// <summary>
/// A lazily-loaded node in the Object Explorer tree. Implements INotifyPropertyChanged
/// (a plain BCL interface, no UI framework reference needed) so WPF's TreeView picks up
/// IsExpanded/IsLoading/IsVisible changes without any manual "refresh the view" call -
/// the old Blazor version's equivalent was StateHasChanged().
/// </summary>
public sealed class TreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isLoading;
    private bool _hasLoadedChildren;
    private bool _isVisible = true;

    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public DbNodeType NodeType { get; set; } = DbNodeType.Folder;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public bool HasLoadedChildren
    {
        get => _hasLoadedChildren;
        set => SetField(ref _hasLoadedChildren, value);
    }

    /// <summary>For filtering/search.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>e.g. "env:{id}|table:dbo.Item" - see TreeNodePayloadParser.</summary>
    public string? Payload { get; set; }

    public ObservableCollection<TreeNode> Children { get; set; } = new();

    /// <summary>
    /// Whether this node should show an expand chevron even before its children have been
    /// loaded - same rule the old web TreeNodeView's IsFolder() used (folder-ish and
    /// table/view nodes yes; columns/triggers/procs/functions no). Info placeholder nodes
    /// ("(no columns found)" etc.) are folders by NodeType but have no payload (or an
    /// "info:" one) and nothing under them, so they're excluded.
    /// </summary>
    public bool CanExpand =>
        NodeType is DbNodeType.Folder or DbNodeType.Root or DbNodeType.SchemaFolder or DbNodeType.Table or DbNodeType.View
        && Payload is not null
        && !Payload.StartsWith("info:", StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
