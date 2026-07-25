namespace SvgLiveEditor.Models;

public sealed record SourceTextEdit(int Start, int Length, string Replacement)
{
    public string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Start < 0 || Length < 0 || Start > source.Length - Length)
        {
            throw new ArgumentOutOfRangeException(nameof(Start));
        }

        return string.Concat(
            source.AsSpan(0, Start),
            Replacement,
            source.AsSpan(Start + Length));
    }
}
