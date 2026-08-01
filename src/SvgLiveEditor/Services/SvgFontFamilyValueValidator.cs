namespace SvgLiveEditor.Services;

public static class SvgFontFamilyValueValidator
{
    public const int MaximumLength = 256;

    public static string? Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SvgFontFamilyStackService.ValidateSerializedValue(value);
    }
}
