using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SyteQuery.Desktop.QueryEditor;

/// <summary>
/// One entry in the IntelliSense popup. <see cref="Text"/> is what AvalonEdit matches
/// against as the user keeps typing; <see cref="_insertText"/> is what actually lands in
/// the document on completion - for snippets those differ (Text is the short prefix shown
/// in the list, insertText is the full expansion template).
/// </summary>
public sealed class SqlCompletionData : ICompletionData
{
    private readonly string _insertText;

    public SqlCompletionData(string text, string kind, string? description = null, string? insertText = null)
    {
        Text = text;
        Kind = kind;
        Description = description ?? "";
        _insertText = insertText ?? text;
    }

    public string Kind { get; }

    public ImageSource? Image => null;

    public string Text { get; }

    /// <summary>What's rendered in the completion list.</summary>
    public object Content => $"{Text}  —  {Kind}";

    public object Description { get; }

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, _insertText);
    }
}
