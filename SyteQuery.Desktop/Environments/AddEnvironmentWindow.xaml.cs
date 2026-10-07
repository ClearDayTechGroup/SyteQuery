using System.Windows;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.Metadata.Services;

namespace SyteQuery.Desktop.Environments;

/// <summary>
/// Add Environment dialog. The flow: add the profile, test the credentials, check the query IDO
/// end to end (the user creates that IDO in SyteLine - see SyteQuery.IDO/README.md), then preload the
/// object lists so Object Explorer's first expand is instant. Any failure rolls the profile back
/// and lets the user fix the problem and retry.
/// </summary>
public partial class AddEnvironmentWindow : Window
{
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly IMetadataCache _metadataCache;

    private EnvProfile _profile = new() { Id = Guid.NewGuid().ToString() };

    public bool WasAdded { get; private set; }

    public AddEnvironmentWindow(IEnvironmentSessionManager envMgr, IMetadataCache metadataCache)
    {
        _envMgr = envMgr;
        _metadataCache = metadataCache;
        InitializeComponent();
    }

    private bool IsInputValid() =>
        !string.IsNullOrWhiteSpace(NameBox.Text) &&
        !string.IsNullOrWhiteSpace(UrlBox.Text) &&
        !string.IsNullOrWhiteSpace(IdoNameBox.Text) &&
        !string.IsNullOrWhiteSpace(UsernameBox.Text) &&
        !string.IsNullOrWhiteSpace(ConfigBox.Text);

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (!IsInputValid())
        {
            ErrorText.Text = "Name, URL, Config, IDO name, and Username are all required.";
            return;
        }

        AddButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ErrorText.Text = "";

        _profile = new EnvProfile
        {
            Id = _profile.Id,
            Name = NameBox.Text.Trim(),
            Url = UrlBox.Text.Trim(),
            Config = ConfigBox.Text.Trim(),
            IdoName = IdoNameBox.Text.Trim(),
            User = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password
        };

        try
        {
            StatusText.Text = "Adding environment...";
            await _envMgr.AddAsync(_profile);

            StatusText.Text = "Testing credentials...";
            var canAuth = await _envMgr.CanAuthenticateAsync(_profile.Id);
            if (!canAuth)
                throw new InvalidOperationException("Authentication failed. Please check your credentials.");

            // The IDO is created by hand in SyteLine (see SyteQuery.IDO/README.md), so check it end to end: it has to
            // exist, have its assembly bound, expose ExecuteQuery, and actually return rows.
            StatusText.Text = $"Checking the query IDO ({_profile.IdoName})...";
            var idoCheck = await _envMgr.ValidateQueryIdoAsync(_profile.Id);
            if (!idoCheck.Ok)
                throw new InvalidOperationException(idoCheck.Problem);

            StatusText.Text = "Loading tables...";
            await _metadataCache.GetTablesAsync(_profile.Id);
            StatusText.Text = "Loading views...";
            await _metadataCache.GetViewsAsync(_profile.Id);
            StatusText.Text = "Loading stored procedures...";
            await _metadataCache.GetStoredProceduresAsync(_profile.Id);
            StatusText.Text = "Loading functions...";
            await _metadataCache.GetScalarFunctionsAsync(_profile.Id);
            await _metadataCache.GetTableValuedFunctionsAsync(_profile.Id);
            await _metadataCache.GetAggregateFunctionsAsync(_profile.Id);

            StatusText.Text = "Complete!";
            WasAdded = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Setup failed: {ex.Message}";
            StatusText.Text = "";

            await _envMgr.RemoveAsync(_profile.Id);
            _metadataCache.InvalidateEnvironment(_profile.Id);

            // Fresh Id for a retry attempt, to avoid a duplicate-key error on the next AddAsync.
            _profile = new EnvProfile { Id = Guid.NewGuid().ToString() };

            AddButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
