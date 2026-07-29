using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class PersianEditorSourceTests
{
    private const string EmptyTextSource = """
        <svg xmlns="http://www.w3.org/2000/svg">
          <g>
            <text x="12" y="24"></text>
          </g>
        </svg>
        """;

    [TestMethod]
    public void RepeatedPersianInsertionAndDeletion_PreservesEveryLogicalLine()
    {
        TextDocument document = new(EmptyTextSource);
        string original = document.Text;
        int insertionOffset = original.IndexOf("</text>", StringComparison.Ordinal);
        const string persian = "سلام دنیا";

        for (int iteration = 0; iteration < 20; iteration++)
        {
            foreach (char character in persian)
            {
                document.Insert(insertionOffset++, character.ToString());
            }

            Assert.AreEqual(persian, document.GetText(
                insertionOffset - persian.Length,
                persian.Length));
            document.Remove(insertionOffset - persian.Length, persian.Length);
            insertionOffset -= persian.Length;
        }

        Assert.AreEqual(original, document.Text);
        Assert.AreEqual(5, document.LineCount);
    }

    [TestMethod]
    public void PersianCompositionUndoRedo_RestoresTheExactSourceAsOneLogicalEdit()
    {
        TextDocument document = new(EmptyTextSource);
        document.UndoStack.MarkAsOriginalFile();
        int insertionOffset = document.Text.IndexOf("</text>", StringComparison.Ordinal);
        const string persian = "متن فارسی — بدون حذف";

        document.UndoStack.StartUndoGroup();
        try
        {
            document.Insert(insertionOffset, persian);
        }
        finally
        {
            document.UndoStack.EndUndoGroup();
        }

        string edited = EmptyTextSource.Insert(insertionOffset, persian);
        Assert.AreEqual(edited, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(EmptyTextSource, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(edited, document.Text);
    }

    [TestMethod]
    public void MixedPersianPunctuationUndoRedoNeverReordersOrDeletesCharacters()
    {
        TextDocument document = new(EmptyTextSource);
        document.UndoStack.MarkAsOriginalFile();
        int insertionOffset =
            document.Text.IndexOf("</text>", StringComparison.Ordinal);
        const string mixed =
            "سلام! من بهروز هستم. نسخه 2.0 آماده است. قیمت: ۱۲۳٬۴۵۶ تومان. (سلام بهروز) Hello — سلام!";

        document.UndoStack.StartUndoGroup();
        try
        {
            document.Insert(insertionOffset, mixed);
        }
        finally
        {
            document.UndoStack.EndUndoGroup();
        }

        string edited = EmptyTextSource.Insert(insertionOffset, mixed);
        Assert.AreEqual(edited, document.Text);
        document.UndoStack.Undo();
        Assert.AreEqual(EmptyTextSource, document.Text);
        document.UndoStack.Redo();
        Assert.AreEqual(edited, document.Text);
    }

    [TestMethod]
    public void DelayedStaleIndexResult_CannotReplaceRapidPersianInput()
    {
        TextDocument document = new(EmptyTextSource);
        SourceRevisionTracker revisions = new();
        revisions.Advance();
        long indexedRevision = revisions.Current;
        string indexedSnapshot = document.Text;
        ManualResetEventSlim releaseIndex = new(initialState: false);
        Task<SvgDocumentIndexResult> delayedIndex = Task.Run(() =>
        {
            releaseIndex.Wait();
            return new SvgDocumentIndexService().Build(indexedSnapshot);
        });

        int insertionOffset = document.Text.IndexOf("</text>", StringComparison.Ordinal);
        const string persian = "تایپ سریع فارسی";
        document.Insert(insertionOffset, persian);
        revisions.Advance();
        string currentSource = document.Text;
        releaseIndex.Set();

        SvgDocumentIndexResult staleResult = delayedIndex.GetAwaiter().GetResult();
        Assert.IsTrue(staleResult.IsIndexed, staleResult.IndexError);
        Assert.IsFalse(revisions.IsCurrent(indexedRevision));
        Assert.AreEqual(currentSource, document.Text);
        StringAssert.Contains(document.Text, persian);
    }

    [TestMethod]
    public void TemporaryInvalidXml_DoesNotChangePersianSource()
    {
        const string temporaryInvalid =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>سلام فارسی</svg>";
        TextDocument document = new(temporaryInvalid);

        SvgDocumentIndexResult result =
            new SvgDocumentIndexService().Build(document.Text);

        Assert.IsFalse(result.Validation.IsValid);
        Assert.IsNull(result.Document);
        Assert.AreEqual(temporaryInvalid, document.Text);
    }
}
