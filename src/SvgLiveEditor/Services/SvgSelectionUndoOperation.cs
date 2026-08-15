using System.Security.Cryptography;
using System.Text;
using ICSharpCode.AvalonEdit.Document;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed record SvgSelectionRestoreTarget(
    SvgMultiSelectionState Selection,
    int SourceLength,
    string SourceSha256)
{
    public static SvgSelectionRestoreTarget Create(
        SvgMultiSelectionState selection,
        string source)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(source);
        return new SvgSelectionRestoreTarget(
            selection,
            source.Length,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))));
    }

    public bool Matches(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length != SourceLength)
        {
            return false;
        }
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(SourceSha256),
                SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class SvgSelectionUndoOperation : IUndoableOperation
{
    private readonly SvgSelectionRestoreTarget _before;
    private readonly SvgSelectionRestoreTarget _after;
    private readonly Action<SvgSelectionRestoreTarget> _requestRestore;

    public SvgSelectionUndoOperation(
        SvgSelectionRestoreTarget before,
        SvgSelectionRestoreTarget after,
        Action<SvgSelectionRestoreTarget> requestRestore)
    {
        _before = before ?? throw new ArgumentNullException(nameof(before));
        _after = after ?? throw new ArgumentNullException(nameof(after));
        _requestRestore = requestRestore
            ?? throw new ArgumentNullException(nameof(requestRestore));
    }

    public void Undo() => _requestRestore(_before);

    public void Redo() => _requestRestore(_after);
}
