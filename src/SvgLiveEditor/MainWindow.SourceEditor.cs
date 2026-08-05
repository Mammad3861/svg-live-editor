using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using SvgLiveEditor.Services;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private void InitializeSourceEditorContextMenu()
    {
        ContextMenu menu = new()
        {
            StaysOpen = false
        };

        foreach (SourceEditorContextMenuItem definition
                 in SourceEditorContextMenuPolicy.Items)
        {
            if (definition.IsSeparator)
            {
                menu.Items.Add(new Separator());
                continue;
            }

            MenuItem item = new()
            {
                Header = definition.Header,
                InputGestureText = definition.InputGestureText,
                Tag = definition.Command
            };
            AutomationProperties.SetName(item, definition.Header);
            item.Click += OnSourceEditorContextCommandClick;
            menu.Items.Add(item);
        }

        SourceEditor.ContextMenu = menu;
        SourceEditor.ContextMenuOpening += OnSourceEditorContextMenuOpening;
        SourceEditor.PreviewMouseRightButtonDown +=
            OnSourceEditorPreviewMouseRightButtonDown;
    }

    private void OnSourceEditorContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        if (SourceEditor.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        SourceEditorCommandState state = new(
            SourceEditor.CanUndo,
            SourceEditor.CanRedo,
            SourceEditor.SelectionLength > 0,
            SourceEditor.Document.TextLength > 0,
            CanPasteSourceText(),
            SourceEditor.IsReadOnly,
            _isEditorTextCompositionActive);
        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is SourceEditorContextCommand command)
            {
                item.IsEnabled = SourceEditorContextMenuPolicy.IsEnabled(
                    command,
                    state);
            }
        }
    }

    private void OnSourceEditorPreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_isEditorTextCompositionActive)
        {
            return;
        }

        TextView textView = SourceEditor.TextArea.TextView;
        Point position = e.GetPosition(textView) + textView.ScrollOffset;
        TextViewPosition? textPosition = textView.GetPositionFloor(position);
        if (textPosition is not TextViewPosition clickedPosition)
        {
            return;
        }

        int offset = SourceEditor.Document.GetOffset(clickedPosition.Location);
        if (SourceEditorContextMenuPolicy.IsOffsetInsideSelection(
                offset,
                SourceEditor.SelectionStart,
                SourceEditor.SelectionLength))
        {
            return;
        }

        SourceEditor.CaretOffset = offset;
        SourceEditor.Select(offset, 0);
    }

    private void OnSourceEditorContextCommandClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem
            {
                Tag: SourceEditorContextCommand command,
                IsEnabled: true
            })
        {
            return;
        }

        switch (command)
        {
            case SourceEditorContextCommand.Undo:
                SourceEditor.Undo();
                break;
            case SourceEditorContextCommand.Redo:
                SourceEditor.Redo();
                break;
            case SourceEditorContextCommand.Cut:
                SourceEditor.Cut();
                break;
            case SourceEditorContextCommand.Copy:
                SourceEditor.Copy();
                break;
            case SourceEditorContextCommand.Paste:
                SourceEditor.Paste();
                break;
            case SourceEditorContextCommand.Delete:
                SourceEditor.Delete();
                break;
            case SourceEditorContextCommand.SelectAll:
                SourceEditor.SelectAll();
                break;
        }
    }

    private static bool CanPasteSourceText()
    {
        try
        {
            return Clipboard.ContainsText(TextDataFormat.UnicodeText);
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
