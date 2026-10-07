using System.Windows;
using System.Windows.Controls;

namespace SyteQuery.Desktop.ObjectExplorer;

/// <summary>
/// A small "pick one of these actions" prompt - the web version used MudBlazor's
/// ShowMessageBox with custom Yes/No button text ("Show Definition" / "Compare"); a
/// MessageBox can't relabel its buttons, so this is the WPF equivalent.
/// </summary>
public partial class ChoiceWindow : Window
{
    /// <summary>Index into the options passed in, or null if the user cancelled.</summary>
    public int? SelectedIndex { get; private set; }

    public ChoiceWindow(string title, string message, params string[] options)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var button = new Button { Content = options[i], Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 0, 8, 0) };
            if (i == 0)
            {
                button.IsDefault = true;
                if (TryFindResource("AccentButtonStyle") is Style accent)
                    button.Style = accent;
            }
            button.Click += (_, _) => { SelectedIndex = index; DialogResult = true; };
            OptionsPanel.Children.Add(button);
        }

        OptionsPanel.Children.Add(new Button { Content = "Cancel", Padding = new Thickness(14, 5, 14, 5), IsCancel = true });
    }

    /// <summary>Convenience wrapper: shows the prompt and returns the chosen index, or null.</summary>
    public static int? Ask(Window owner, string title, string message, params string[] options)
    {
        var dialog = new ChoiceWindow(title, message, options) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedIndex : null;
    }
}
