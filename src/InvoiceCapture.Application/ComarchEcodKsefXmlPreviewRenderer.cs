using System.Text.Json;
using System.Xml.Linq;
using System.Xml;

namespace InvoiceCapture.Application;

public static class ComarchEcodKsefXmlPreviewRenderer
{
    public const string ProfileId = ComarchInvoiceProfiles.Ksef;

    public static ComarchXmlPreviewResult Render(JsonElement extraction)
    {
        if (!extraction.TryGetProperty("comarchEcodKsef", out var invoice) || invoice.ValueKind != JsonValueKind.Object)
        {
            return new ComarchXmlPreviewResult(null, null, "This older extraction does not contain the Comarch ECOD/KSeF 7.77 profile. Restart processing to create it.");
        }

        var profileId = GetString(invoice, "profile");
        if (!ComarchInvoiceProfiles.IsSupported(profileId))
        {
            return new ComarchXmlPreviewResult(null, null, "The extraction contains an unsupported Comarch profile.");
        }

        if (!invoice.TryGetProperty("xml", out var xml) || xml.ValueKind != JsonValueKind.Object)
        {
            return new ComarchXmlPreviewResult(null, profileId, "The extraction does not contain the full Comarch XML mapping. Restart processing to create it.");
        }

        try
        {
            var root = ToElement(xml);
            if (!string.Equals(root.Name.LocalName, "Document-Invoice", StringComparison.Ordinal))
            {
                return new ComarchXmlPreviewResult(null, profileId, "The full Comarch XML mapping must start with Document-Invoice.");
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return new ComarchXmlPreviewResult(document.ToString(), profileId, null);
        }
        catch (ArgumentException exception)
        {
            return new ComarchXmlPreviewResult(null, profileId, exception.Message);
        }
    }

    private static XElement ToElement(JsonElement node)
    {
        var name = GetString(node, "name");
        if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentException("Every Comarch XML node requires a name."); }
        XmlConvert.VerifyNCName(name);
        var element = new XElement(name);
        if (node.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind != JsonValueKind.String) { throw new ArgumentException($"Comarch XML node {name} has a non-text value."); }
            element.Value = value.GetString() ?? string.Empty;
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray()) { element.Add(ToElement(child)); }
        }

        return element;
    }

    private static string? GetString(JsonElement element, string propertyName) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : null;
}
