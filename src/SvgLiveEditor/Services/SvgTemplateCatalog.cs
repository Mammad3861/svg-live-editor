using System.IO;
using System.Reflection;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class SvgTemplateCatalog
{
    private static readonly TemplateResource[] Resources =
    [
        new(
            "blank",
            "Blank Canvas",
            "Starter",
            "800 × 600",
            "A minimal 800 × 600 SVG with a matching viewBox.",
            "SvgLiveEditor.Templates.blank.svg"),
        new(
            "app-icon",
            "App Icon",
            "Icon",
            "24 × 24",
            "A compact icon composition built from safe vector shapes.",
            "SvgLiveEditor.Templates.app-icon.svg"),
        new(
            "social-card",
            "Social Card",
            "Layout",
            "1200 × 630",
            "A social sharing card with editable title and accent shapes.",
            "SvgLiveEditor.Templates.social-card.svg"),
        new(
            "flow-diagram",
            "Flow Diagram",
            "Diagram",
            "960 × 540",
            "A three-step flow with an internal marker reference.",
            "SvgLiveEditor.Templates.flow-diagram.svg"),
        new(
            "persian-rtl",
            "Persian / RTL",
            "Typography",
            "1000 × 500",
            "An original Persian text layout using safe RTL attributes.",
            "SvgLiveEditor.Templates.persian-rtl.svg")
    ];

    private readonly Assembly _assembly;
    private readonly SvgValidationService _validationService;

    public SvgTemplateCatalog()
        : this(typeof(SvgTemplateCatalog).Assembly, new SvgValidationService())
    {
    }

    public SvgTemplateCatalog(
        Assembly assembly,
        SvgValidationService validationService)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _validationService = validationService
            ?? throw new ArgumentNullException(nameof(validationService));
    }

    public IReadOnlyList<SvgTemplateDefinition> LoadAll()
    {
        List<SvgTemplateDefinition> templates = [];
        foreach (TemplateResource resource in Resources)
        {
            string source = ReadResource(resource.ResourceName);
            SvgValidationResult validation = _validationService.Validate(source);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Bundled template '{resource.Id}' failed secure SVG validation: {validation.Message}");
            }

            templates.Add(new SvgTemplateDefinition(
                resource.Id,
                resource.Name,
                resource.Category,
                resource.Dimensions,
                resource.Description,
                source));
        }

        return templates.AsReadOnly();
    }

    private string ReadResource(string resourceName)
    {
        using Stream stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded template '{resourceName}' was not found.");
        using StreamReader reader = new(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private sealed record TemplateResource(
        string Id,
        string Name,
        string Category,
        string Dimensions,
        string Description,
        string ResourceName);
}
