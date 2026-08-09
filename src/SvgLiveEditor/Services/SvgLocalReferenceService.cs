using System.Text.RegularExpressions;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

internal sealed class SvgLocalReferenceService
{
    private static readonly Regex CssUrlPattern = new(
        "url\\s*\\(\\s*(['\"]?)(?<target>.*?)\\1\\s*\\)",
        RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant
        | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex IdReferenceTokenPattern = new(
        "(?<target>[^\\s]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public IReadOnlyList<SvgLocalReference> FindReferences(
        IEnumerable<SvgElementNode> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        List<SvgLocalReference> references = [];
        try
        {
            foreach (SvgElementNode element in elements)
            {
                foreach (SvgAttributeSpan attribute in element.Attributes)
                {
                    if (ContainsEncodedLocalReference(attribute))
                    {
                        throw new InvalidOperationException(
                            "Encoded local SVG references cannot be analyzed or rewritten safely.");
                    }

                    HashSet<string> targets = new(StringComparer.Ordinal);
                    if (attribute.Name.Equals("href", StringComparison.Ordinal))
                    {
                        string href = attribute.RawValue.Trim();
                        if (href.Length > 1 && href[0] == '#')
                        {
                            targets.Add(href[1..]);
                        }
                    }
                    if (IsIdReferenceAttribute(attribute.Name))
                    {
                        foreach (Match match in IdReferenceTokenPattern.Matches(
                                     attribute.RawValue))
                        {
                            targets.Add(match.Groups["target"].Value);
                        }
                    }

                    foreach (Match match in CssUrlPattern.Matches(attribute.RawValue))
                    {
                        string target = match.Groups["target"].Value.Trim();
                        if (target.Length > 1 && target[0] == '#')
                        {
                            targets.Add(target[1..]);
                        }
                    }

                    references.AddRange(targets.Select(target =>
                        new SvgLocalReference(element, attribute, target)));
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new InvalidOperationException(
                "Local SVG references are too complex to analyze safely.",
                exception);
        }

        return references;
    }

    public string RewriteReferences(
        SvgAttributeSpan attribute,
        IReadOnlyDictionary<string, string> idMap)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(idMap);

        string rewritten;
        try
        {
            rewritten = CssUrlPattern.Replace(
                attribute.RawValue,
                match => RewriteUrlMatch(match, idMap));
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new InvalidOperationException(
                "Local SVG references are too complex to rewrite safely.",
                exception);
        }
        if (!attribute.Name.Equals("href", StringComparison.Ordinal))
        {
            return IsIdReferenceAttribute(attribute.Name)
                ? RewriteIdReferenceTokens(rewritten, idMap)
                : rewritten;
        }

        int first = 0;
        while (first < rewritten.Length && char.IsWhiteSpace(rewritten[first]))
        {
            first++;
        }
        int last = rewritten.Length;
        while (last > first && char.IsWhiteSpace(rewritten[last - 1]))
        {
            last--;
        }
        if (last - first <= 1 || rewritten[first] != '#')
        {
            return rewritten;
        }

        string target = rewritten[(first + 1)..last];
        return idMap.TryGetValue(target, out string? replacement)
            ? string.Concat(
                rewritten.AsSpan(0, first + 1),
                replacement,
                rewritten.AsSpan(last))
            : rewritten;
    }

    private static bool IsIdReferenceAttribute(string name) =>
        name is "aria-labelledby" or "aria-describedby"
            or "aria-activedescendant";

    private static bool ContainsEncodedLocalReference(
        SvgAttributeSpan attribute) =>
        attribute.RawValue.Contains('&', StringComparison.Ordinal)
        && (attribute.Name.Equals("href", StringComparison.Ordinal)
            || IsIdReferenceAttribute(attribute.Name)
            || attribute.RawValue.Contains(
                "url",
                StringComparison.OrdinalIgnoreCase));

    private static string RewriteIdReferenceTokens(
        string value,
        IReadOnlyDictionary<string, string> idMap)
    {
        try
        {
            return IdReferenceTokenPattern.Replace(
                value,
                match => idMap.TryGetValue(
                    match.Groups["target"].Value,
                    out string? replacement)
                        ? replacement
                        : match.Value);
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new InvalidOperationException(
                "Local SVG ID references are too complex to rewrite safely.",
                exception);
        }
    }

    private static string RewriteUrlMatch(
        Match match,
        IReadOnlyDictionary<string, string> idMap)
    {
        Group targetGroup = match.Groups["target"];
        string trimmed = targetGroup.Value.Trim();
        if (trimmed.Length <= 1
            || trimmed[0] != '#'
            || !idMap.TryGetValue(trimmed[1..], out string? replacement))
        {
            return match.Value;
        }

        int leadingWhitespace = targetGroup.Value.Length
            - targetGroup.Value.TrimStart().Length;
        int trailingStart = targetGroup.Value.TrimEnd().Length;
        string newTarget = string.Concat(
            targetGroup.Value.AsSpan(0, leadingWhitespace),
            "#",
            replacement,
            targetGroup.Value.AsSpan(trailingStart));
        int targetOffset = targetGroup.Index - match.Index;
        return string.Concat(
            match.Value.AsSpan(0, targetOffset),
            newTarget,
            match.Value.AsSpan(targetOffset + targetGroup.Length));
    }
}

internal sealed record SvgLocalReference(
    SvgElementNode Element,
    SvgAttributeSpan Attribute,
    string TargetId);
