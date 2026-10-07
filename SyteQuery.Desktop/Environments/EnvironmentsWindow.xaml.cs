using System.Collections.ObjectModel;
using System.Windows;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.Metadata.Services;

namespace SyteQuery.Desktop.Environments;

/// <summary>
/// Management shell for environments - list + Add/Edit/Remove, replacing the direct
/// "Tools > Environments..." -> AddEnvironmentWindow shortcut now that there's more than
/// one thing to do with an environment. Add and Edit still validate/save through
/// IEnvironmentSessionManager exactly as before; this window just doesn't need to react to
/// their results itself - Add/Edit/Remove all fire IEnvironmentSessionManager.
/// ProfilesChanged, which ObjectExplorerViewModel and QueryEditorViewModel already
/// subscribe to independently, so the tree and the query editor's environment picker pick
/// up changes without this window knowing about either of them.
/// </summary>
public partial class EnvironmentsWindow : Window
{
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly IMetadataCache _metadataCache;

    public ObservableCollection<EnvProfile> Items { get; } = new();

    public EnvironmentsWindow(IEnvironmentSessionManager envMgr, IMetadataCache metadataCache)
    {
        _envMgr = envMgr;
        _metadataCache = metadataCache;
        InitializeComponent();
        DataContext = this;

        _envMgr.ProfilesChanged += RefreshList;
        Closed += (_, _) => _envMgr.ProfilesChanged -= RefreshList;

        RefreshList();
    }

    private void RefreshList()
    {
        var selectedId = (Grid.SelectedItem as EnvProfile)?.Id;

        Items.Clear();
        foreach (var profile in _envMgr.Profiles)
            Items.Add(profile);

        Grid.SelectedItem = selectedId != null ? Items.FirstOrDefault(p => p.Id == selectedId) : null;
    }

    private void OnGridSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = Grid.SelectedItem is EnvProfile;
        EditButton.IsEnabled = hasSelection;
        RemoveButton.IsEnabled = hasSelection;
    }

    private void OnGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is EnvProfile)
            OpenEditDialog();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEnvironmentWindow(_envMgr, _metadataCache) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnEditClick(object sender, RoutedEventArgs e) => OpenEditDialog();

    private void OpenEditDialog()
    {
        if (Grid.SelectedItem is not EnvProfile selected)
            return;

        var dialog = new EditEnvironmentWindow(_envMgr, selected) { Owner = this };
        dialog.ShowDialog();
    }

    private async void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not EnvProfile selected)
            return;

        var confirm = MessageBox.Show(
            $"Delete environment '{selected.Name}'? This cannot be undone.",
            "Delete Environment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            await _envMgr.RemoveAsync(selected.Id);
            _metadataCache.InvalidateEnvironment(selected.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete environment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
