using System.Xml.Linq;
using InvoiceCapture.Application;

namespace InvoiceCapture.Infrastructure;

public sealed class ComarchKsefSchemaGuideProvider
{
    private static readonly IReadOnlyList<(string Profile, string FileName)> Profiles =
    [
        (ComarchInvoiceProfiles.Invoice, "comarch-edi-invoice-7.77-invoice.xsd"),
        (ComarchInvoiceProfiles.Correction, "comarch-edi-invoice-7.77-correction.xsd"),
        (ComarchInvoiceProfiles.Ksef, "comarch-edi-invoice-7.77-ksef.xsd"),
        (ComarchInvoiceProfiles.KsefCorrection, "comarch-edi-invoice-7.77-ksef_correction.xsd")
    ];
    private static readonly XNamespace SchemaNamespace = "http://www.w3.org/2001/XMLSchema";
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private string? guide;

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (guide is not null) { return guide; }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (guide is not null) { return guide; }
            var paths = new List<string>();
            foreach (var profile in Profiles)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "specs", "ComarchInvoice", profile.FileName);
                await using var stream = new FileStream(path, new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.Read
                });
                var schema = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                var root = schema.Root?.Elements(SchemaNamespace + "element").SingleOrDefault(x => string.Equals((string?)x.Attribute("name"), "Document-Invoice", StringComparison.Ordinal));
                if (root is null) { throw new InvalidDataException($"The deployed Comarch XSD for {profile.Profile} has no Document-Invoice root."); }
                paths.Add($"PROFILE {profile.Profile}");
                AddPaths(root, string.Empty, paths);
            }
            guide = string.Join('\n', paths);
            return guide;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private static void AddPaths(XElement element, string parentPath, ICollection<string> paths)
    {
        var name = (string?)element.Attribute("name") ?? throw new InvalidDataException("An XSD element has no name.");
        var path = $"{parentPath}/{name}";
        var minimum = (string?)element.Attribute("minOccurs") ?? "1";
        var maximum = (string?)element.Attribute("maxOccurs") ?? "1";
        paths.Add($"{path} [{minimum}..{maximum}]");

        var sequence = element.Element(SchemaNamespace + "complexType")?.Element(SchemaNamespace + "sequence");
        if (sequence is null) { return; }
        foreach (var child in sequence.Elements(SchemaNamespace + "element"))
        {
            AddPaths(child, path, paths);
        }
    }
}
