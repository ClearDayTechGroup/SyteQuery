using System.Windows;
using SyteQuery.Features.Environments.Services;

namespace SyteQuery.Desktop.Environments;

/// <summary>
/// Edit an environment's connection details. Unlike AddEnvironmentWindow this doesn't re-test
/// authentication or roll anything back - it's a field update - but if anything that decides where
/// queries run changed (URL, config, user, password, IDO name) it re-checks the query IDO afterwards
/// and warns if that fails. The password field starts blank and is only changed if the user types a
/// new one: the current password isn't kept anywhere but encrypted, so it can't be shown back, and
/// forcing it to be re-entered on every edit would be a bad time.
/// </summary>
public partial class EditEnvironmentWindow : Window
{
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly EnvProfile _original;

    public EditEnvironmentWindow(IEnvironmentSessionManager envMgr, EnvProfile original)
    {
        _envMgr = envMgr;
        _original = original;
        InitializeComponent();

        NameBox.Text = original.Name;
        UrlBox.Text = original.Url;
        ConfigBox.Text = original.Config;
        IdoNameBox.Text = original.IdoName;
        UsernameBox.Text = original.User;
    }

    private bool IsInputValid() =>
        !string.IsNullOrWhiteSpace(NameBox.Text) &&
        !string.IsNullOrWhiteSpace(UrlBox.Text) &&
        !string.IsNullOrWhiteSpace(IdoNameBox.Text) &&
        !string.IsNullOrWhiteSpace(UsernameBox.Text) &&
        !string.IsNullOrWhiteSpace(ConfigBox.Text);

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!IsInputValid())
        {
            ErrorText.Text = "Name, URL, Config, IDO name, and Username are all required.";
            return;
        }

        SaveButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ErrorText.Text = "";

        var updated = new EnvProfile
        {
            Id = _original.Id,
            Name = NameBox.Text.Trim(),
            Url = UrlBox.Text.Trim(),
            Config = ConfigBox.Text.Trim(),
            IdoName = IdoNameBox.Text.Trim(),
            User = UsernameBox.Text.Trim(),
            Password = string.IsNullOrEmpty(PasswordBox.Password) ? _original.Password : PasswordBox.Password
        };

        try
        {
            await _envMgr.UpdateAsync(updated);

            // Saved either way: a failed check is a warning, not a rollback (the user may be fixing the IDO
            // in SyteLine right now), but better to say so now than to let the next query fail mysteriously.
            var connectionChanged =
                updated.Url != _original.Url ||
                updated.Config != _original.Config ||
                updated.User != _original.User ||
                updated.Password != _original.Password ||
                updated.IdoName != _original.IdoName;

            if (connectionChanged)
            {
                ErrorText.Text = "Saved. Checking the query IDO...";
                var idoCheck = await _envMgr.ValidateQueryIdoAsync(updated.Id);
                if (!idoCheck.Ok)
                {
                    MessageBox.Show(
                        this,
                        "Your changes were saved, but the query IDO check failed:\n\n" + idoCheck.Problem,
                        "Query IDO check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else if (idoCheck.Warning is not null)
                {
                    MessageBox.Show(this, idoCheck.Warning, "Query IDO", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Failed to save: {ex.Message}";
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
