using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using SyteQuery.Desktop.Snippets;
using SyteQuery.Desktop.Theming;
using System.Windows.Media;
using System.Xml;

namespace SyteQuery.Desktop.QueryEditor;

/// <summary>
/// Code-behind owns everything AvalonEdit-specific (syntax highlighting setup, the
/// completion popup, comment/uncomment text manipulation) since none of that is naturally
/// XAML-bindable; QueryEditorViewModel stays a plain, AvalonEdit-agnostic ViewModel that
/// only knows about SQL text in/out.
/// </summary>
public partial class QueryEditorView : UserControl
{
    private CompletionWindow? _completionWindow;
    private readonly IHighlightingDefinition _highlighting;

    public QueryEditorView()
    {
        InitializeComponent();
        _highlighting = LoadSqlHighlighting();
        Editor.SyntaxHighlighting = _highlighting;
        ApplyTheme();
        ThemeService.ThemeChanged += ApplyTheme;
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.PreviewKeyDown += OnPreviewKeyDown;
    }

    private QueryEditorViewModel? ViewModel => DataContext as QueryEditorViewModel;

    /// <summary>The ViewModel this tab is bound to - MainWindow needs it to track which tab is
    /// active (for the shared Messages/Results panels) and to switch its environment.</summary>
    public QueryEditorViewModel? Model => ViewModel;

    /// <summary>Called from MainWindow when an Object Explorer node is selected - switches
    /// the active environment and, for a table/view, drops in a quick "select top 100"
    /// query. Passing a null <paramref name="sql"/> only switches the environment.</summary>
    public void SetEnvironmentAndQuery(string envId, string? sql)
    {
        ViewModel?.SetActiveEnvironment(envId);
        if (sql != null)
            Editor.Text = sql;
    }

    /// <summary>Called from MainWindow when a snippet is loaded from the docked Snippets
    /// panel - unlike SetEnvironmentAndQuery, loading a snippet doesn't touch the active
    /// environment (the old QueryTool.LoadSnippetAsync didn't either).</summary>
    public void SetQueryText(string sql) => Editor.Text = sql;

    /// <summary>Editor surface + syntax colors per Windows light/dark mode. AvalonEdit isn't
    /// covered by WPF's Fluent theme, and the XSHD's default palette is tuned for dark, so
    /// both are set explicitly here and re-applied when the system theme flips (the control
    /// lives for the whole app lifetime, so it never needs to unsubscribe).</summary>
    private void ApplyTheme()
    {
        var dark = ThemeService.IsDark;
        static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        Editor.Background = new SolidColorBrush(C(dark ? "#1E1E1E" : "#FFFFFF"));
        Editor.Foreground = new SolidColorBrush(C(dark ? "#D4D4D4" : "#1F1F1F"));
        Editor.LineNumbersForeground = new SolidColorBrush(C(dark ? "#858585" : "#9A9A9A"));

        void Set(string name, string darkHex, string lightHex)
        {
            if (_highlighting.GetNamedColor(name) is { } color)
                color.Foreground = new SimpleHighlightingBrush(C(dark ? darkHex : lightHex));
        }

        Set("Comment", "#57A64A", "#008000");
        Set("String", "#D69D85", "#A31515");
        Set("Keyword", "#569CD6", "#0000FF");
        Set("FunctionKeyword", "#DCDCAA", "#795E26");
        Set("Number", "#B5CEA8", "#098658");
        Set("Variable", "#9CDCFE", "#001080");
        Set("BracketedIdentifier", "#D7BA7D", "#811F3F");

        // Highlighting colors are read when lines render - nudge a redraw.
        Editor.TextArea.TextView.Redraw();
    }

    private static IHighlightingDefinition LoadSqlHighlighting()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "SyteQuery.Desktop.QueryEditor.SqlHighlighting.xshd";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new XmlTextReader(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    // ------------------------------------------------------------
    // Toolbar
    // ------------------------------------------------------------

    private async void OnExecuteClick(object sender, RoutedEventArgs e) => await ExecuteAsync();

    private async Task ExecuteAsync()
    {
        if (ViewModel is not { } vm)
            return;

        await vm.ExecuteAsync(Editor.Text);
    }

    private void OnDismissErrorClick(object sender, RoutedEventArgs e) => ViewModel?.DismissError();

    private void OnFormatClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        Editor.Text = vm.Format(Editor.Text);
    }

    private async void OnSaveSnippetClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        var categories = await vm.SnippetService.GetCategoriesAsync();
        var dialog = new SnippetEditWindow(vm.SnippetService, categories, original: null, initialQuery: Editor.Text)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
            vm.NotifySnippetSaved();
    }

    private void OnCommentClick(object sender, RoutedEventArgs e) => ToggleCommentSelection(comment: true);

    private void OnUncommentClick(object sender, RoutedEventArgs e) => ToggleCommentSelection(comment: false);

    /// <summary>Line-prefix comment toggle ("-- "), same convention SSMS uses - no
    /// dependency on the old Monaco JS interop, just direct line-by-line text edits.</summary>
    private void ToggleCommentSelection(bool comment)
    {
        var doc = Editor.Document;
        var selection = Editor.TextArea.Selection;

        int startOffset = selection.IsEmpty ? Editor.CaretOffset : selection.SurroundingSegment.Offset;
        int endOffset = selection.IsEmpty ? Editor.CaretOffset : selection.SurroundingSegment.EndOffset;

        int startLine = doc.GetLineByOffset(startOffset).LineNumber;
        int endLine = doc.GetLineByOffset(endOffset).LineNumber;

        doc.BeginUpdate();
        try
        {
            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                var line = doc.GetLineByNumber(lineNumber);
                var text = doc.GetText(line.Offset, line.Length);

                if (comment)
                {
                    doc.Insert(line.Offset, "-- ");
                }
                else if (text.TrimStart().StartsWith("--"))
                {
                    var dashIndex = text.IndexOf("--", StringComparison.Ordinal);
                    var afterDashes = dashIndex + 2;
                    var removeLength = afterDashes < text.Length && text[afterDashes] == ' ' ? afterDashes + 1 : afterDashes;
                    doc.Remove(line.Offset, removeLength);
                }
            }
        }
        finally
        {
            doc.EndUpdate();
        }
    }

    // ------------------------------------------------------------
    // IntelliSense
    // ------------------------------------------------------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            _ = ExecuteAsync();
        }
        else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = ShowGeneralCompletionAsync();
        }
    }

    private async void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == ".")
        {
            await ShowColumnCompletionAsync();
            return;
        }

        // Auto-trigger like the old Monaco-based editor did: as soon as the first letter of
        // a new word is typed, open the popup - AvalonEdit then filters it live against
        // whatever's typed next on its own, so this only needs to fire once per word, not
        // on every keystroke. A window already open (mid-word) keeps filtering itself.
        if (_completionWindow is null && e.Text.Length == 1 && IsIdentifierChar(e.Text[0]) && !char.IsDigit(e.Text[0]))
        {
            var caret = Editor.CaretOffset;
            var precededByIdentifierChar = caret >= 2 && IsIdentifierChar(Editor.Document.GetCharAt(caret - 2));
            if (!precededByIdentifierChar)
                await ShowGeneralCompletionAsync();
        }
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Walks left from the caret over identifier characters to find where the
    /// current word started - used so a completion window opened mid-word (Ctrl+Space, or
    /// the auto-trigger above) filters/replaces the whole word, not just what's typed
    /// after the window appears.</summary>
    private int GetWordStartOffset()
    {
        var doc = Editor.Document;
        var offset = Editor.CaretOffset;
        while (offset > 0 && IsIdentifierChar(doc.GetCharAt(offset - 1)))
            offset--;
        return offset;
    }

    private async Task ShowGeneralCompletionAsync()
    {
        if (ViewModel is not { } vm)
            return;

        var data = await vm.GetCompletionDataAsync();
        if (data is null)
            return;

        var window = OpenCompletionWindow();
        var items = window.CompletionList.CompletionData;

        foreach (var kw in data.Keywords)
            items.Add(new SqlCompletionData(kw.Label, "Keyword", kw.Documentation ?? kw.Detail));
        foreach (var t in data.Tables)
            items.Add(new SqlCompletionData(t.Label, "Table", t.Detail));
        foreach (var v in data.Views)
            items.Add(new SqlCompletionData(v.Label, "View", v.Detail));
        foreach (var sp in data.StoredProcedures)
            items.Add(new SqlCompletionData(sp.Label, "Stored Procedure", sp.Detail));
        foreach (var fn in data.Functions)
            items.Add(new SqlCompletionData(fn.Label, "Function", fn.Detail));
        foreach (var snippet in data.Snippets)
            items.Add(new SqlCompletionData(snippet.Prefix, "Snippet", snippet.Description, insertText: snippet.Template));

        window.Show();
    }

    private async Task ShowColumnCompletionAsync()
    {
        if (ViewModel is not { } vm)
            return;

        var identifier = GetIdentifierBeforeCaret();
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        var columns = await vm.GetTableColumnsAsync(identifier);
        if (columns.Count == 0)
            return;

        var window = OpenCompletionWindow();
        foreach (var col in columns)
            window.CompletionList.CompletionData.Add(new SqlCompletionData(col.Name, "Column", col.Detail));
        window.Show();
    }

    /// <summary>Reads the dotted/word identifier immediately before the caret (which, when
    /// this is called, sits right after the '.' the user just typed) - e.g. for
    /// "...dbo.Item." it returns "dbo.Item".</summary>
    private string GetIdentifierBeforeCaret()
    {
        var doc = Editor.Document;
        int dotOffset = Editor.CaretOffset - 1;
        if (dotOffset < 0 || doc.GetCharAt(dotOffset) != '.')
            return "";

        int start = dotOffset;
        bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';
        while (start > 0 && IsIdentChar(doc.GetCharAt(start - 1)))
            start--;

        return doc.GetText(start, dotOffset - start);
    }

    private CompletionWindow OpenCompletionWindow()
    {
        _completionWindow?.Close();
        var window = new CompletionWindow(Editor.TextArea)
        {
            StartOffset = GetWordStartOffset()
        };
        _completionWindow = window;
        window.Closed += (_, _) => _completionWindow = null;
        return window;
    }
}
