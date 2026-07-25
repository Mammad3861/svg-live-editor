using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class AvalonEditDocumentEditService
{
    public void Apply(TextDocument document, SourceTextEdit edit)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edit);

        if (edit.Start < 0
            || edit.Length < 0
            || edit.Start > document.TextLength - edit.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(edit));
        }

        document.UndoStack.StartUndoGroup();
        try
        {
            document.Replace(edit.Start, edit.Length, edit.Replacement);
        }
        finally
        {
            document.UndoStack.EndUndoGroup();
        }
    }
}
