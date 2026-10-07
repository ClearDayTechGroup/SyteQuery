using System.Windows;
using Microsoft.Extensions.Configuration;
using Velopack;
using Velopack.Sources;

namespace SyteQuery.Desktop.Updates;

/// <summary>
/// Help > Check for Updates. Uses Velopack to look for a newer release in a GitHub repository's
/// Releases, download it, and restart into it. Does nothing useful until two things are true, and
/// says so rather than failing silently: the app was installed by the Velopack installer (a plain
/// build from Visual Studio can't replace itself), and <c>Updates:GitHubRepoUrl</c> in
/// appsettings.json points at the repository that publishes releases.
/// </summary>
public sealed class UpdateService
{
    private readonly string? _repoUrl;

    public UpdateService(IConfiguration configuration)
    {
        _repoUrl = configuration["Updates:GitHubRepoUrl"];
    }

    public async Task CheckForUpdatesAsync(Window owner)
    {
        if (string.IsNullOrWhiteSpace(_repoUrl))
        {
            Info(owner, "No update feed is configured yet.\n\nSet Updates:GitHubRepoUrl in appsettings.json to the GitHub repository that publishes SyteQuery releases.");
            return;
        }

        try
        {
            var manager = new UpdateManager(new GithubSource(_repoUrl, accessToken: null, prerelease: false));

            if (!manager.IsInstalled)
            {
                Info(owner, "This copy of SyteQuery wasn't installed with the installer, so it can't update itself.\n\nInstall it with SyteQuery-Setup.exe to get automatic updates.");
                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                Info(owner, $"You're up to date (version {manager.CurrentVersion}).");
                return;
            }

            var answer = MessageBox.Show(
                owner,
                $"Version {update.TargetFullRelease.Version} is available (you have {manager.CurrentVersion}).\n\nDownload it and restart SyteQuery now?",
                "Update available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            await manager.DownloadUpdatesAsync(update);
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Couldn't check for updates: {ex.Message}", "Check for updates",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void Info(Window owner, string message) =>
        MessageBox.Show(owner, message, "Check for updates", MessageBoxButton.OK, MessageBoxImage.Information);
}
