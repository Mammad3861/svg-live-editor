namespace SvgLiveEditor.Models;

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool Contains(int offset) => offset >= Start && offset < End;
}
