using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SyteQuery.Desktop.Theming;

public enum ThemeChoice { System, Light, Dark }

/// <summary>
/// Owns the app's light/dark choice: "System" follows Windows live, "Light"/"Dark" force it.
/// WPF's own Fluent theme is switched through Application.ThemeMode; the parts it doesn't
/// reach (AvalonDock's chrome, AvalonEdit's syntax colors) listen to <see cref="ThemeChanged"/>
/// and read <see cref="IsDark"/> instead. The choice is saved to a small file next to the
/// app's database so it survives restarts.
/// </summary>
public static class ThemeService
{
    private static readonly string SettingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SyteQuery", "theme.txt");

    // Order matters: static initializers run top to bottom, and IsDark depends on Choice.
    public static ThemeChoice Choice { get; private set; } = LoadChoice();
    public static bool IsDark { get; private set; } = Resolve(Choice);

    /// <summary>Raised (on the UI thread) after the effective light/dark mode changes.</summary>
    public static event Action? ThemeChanged;

    static ThemeService()
    {
        // Only matters while following Windows; an explicit Light/Dark ignores the system.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (Choice != ThemeChoice.System ||
                e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
                return;

            var dark = ReadSystemIsDark();
            if (dark == IsDark)
                return;

            IsDark = dark;
            // SystemEvents fires on its own thread - marshal to the UI thread for subscribers.
            Application.Current?.Dispatcher.BeginInvoke(() => ThemeChanged?.Invoke());
        };
    }

    /// <summary>Applies the saved choice to the WPF theme - call once at startup, before any
    /// window is shown.</summary>
    public static void ApplySavedChoice() => ApplyToApplication(Choice);

    public static void SetChoice(ThemeChoice choice)
    {
        Choice = choice;
        Save(choice);
        ApplyToApplication(choice);
        IsDark = Resolve(choice);
        ThemeChanged?.Invoke();
    }

    private static void ApplyToApplication(ThemeChoice choice)
    {
        if (Application.Current is null)
            return;

        Application.Current.ThemeMode = choice switch
        {
            ThemeChoice.Light => ThemeMode.Light,
            ThemeChoice.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }

    private static bool Resolve(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => false,
        ThemeChoice.Dark => true,
        _ => ReadSystemIsDark()
    };

    private static bool ReadSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static ThemeChoice LoadChoice()
    {
        try
        {
            if (File.Exists(SettingPath) && Enum.TryParse<ThemeChoice>(File.ReadAllText(SettingPath).Trim(), out var saved))
                return saved;
        }
        catch
        {
            // A missing/unreadable settings file just means "use the default".
        }

        return ThemeChoice.System;
    }

    private static void Save(ThemeChoice choice)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingPath)!);
            File.WriteAllText(SettingPath, choice.ToString());
        }
        catch
        {
            // Not being able to remember the choice shouldn't break switching it.
        }
    }
}
