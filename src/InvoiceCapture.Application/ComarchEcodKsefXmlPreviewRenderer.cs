using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace InvoiceCapture.Application;

public static class ComarchEcodKsefXmlPreviewRenderer
{
    public const string ProfileId = ComarchInvoiceProfiles.Ksef;

    public static ComarchXmlPreviewResult Render(JsonElement extraction) =>
        extraction.TryGetProperty("invoice", out var invoice) && invoice.ValueKind == JsonValueKind.Object
            ? RenderFacts(extraction, invoice)
            : RenderLegacyTree(extraction);

    private static ComarchXmlPreviewResult RenderFacts(JsonElement extraction, JsonElement invoice)
    {
        var profileId = GetString(extraction, "profile");
        if (!ComarchInvoiceProfiles.IsSupported(profileId))
        {
            return new ComarchXmlPreviewResult(null, null, "The normalized extraction has no supported Comarch profile.");
        }

        var supportedProfile = profileId!;
        var missing = MissingRequiredFacts(invoice);
        if (missing.Count > 0)
        {
            return new ComarchXmlPreviewResult(null, supportedProfile, $"Deterministic Comarch mapping is blocked by missing or ambiguous facts: {string.Join(", ", missing)}.");
        }

        var lines = GetArray(invoice, "lines");
        if (supportedProfile is ComarchInvoiceProfiles.Invoice or ComarchInvoiceProfiles.Correction && lines.Count == 0)
        {
            return new ComarchXmlPreviewResult(null, supportedProfile, "The selected Comarch profile requires at least one invoice line.");
        }

        if (lines.Any(line => !IsCompleteLine(line)))
        {
            return new ComarchXmlPreviewResult(null, supportedProfile, "Deterministic Comarch mapping is blocked by an incomplete invoice line.");
        }

        var summary = GetObject(invoice, "summary");
        var root = new XElement("Document-Invoice",
            BuildHeader(invoice, supportedProfile),
            new XElement("Invoice-Parties", BuildParty("Buyer", GetObject(invoice, "buyer")), BuildParty("Seller", GetObject(invoice, "seller"))));

        if (lines.Count > 0)
        {
            root.Add(new XElement("Invoice-Lines", lines.Select(BuildLine)));
        }

        root.Add(BuildSummary(summary));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return new ComarchXmlPreviewResult(document.ToString(), supportedProfile, null);
    }

    private static XElement BuildHeader(JsonElement invoice, string profileId)
    {
        var header = new XElement("Invoice-Header",
            Element("InvoiceNumber", GetString(invoice, "invoiceNumber")),
            Element("InvoiceDate", GetString(invoice, "invoiceDate")));
        if (profileId is ComarchInvoiceProfiles.Ksef or ComarchInvoiceProfiles.KsefCorrection)
        {
            AddIfPresent(header, "KSEFDocumentNumber", GetString(invoice, "ksefDocumentNumber"));
        }

        AddIfPresent(header, "SalesDate", GetString(invoice, "salesDate"));
        AddIfPresent(header, "InvoicingPeriod", GetString(invoice, "invoicingPeriod"));
        header.Add(Element("InvoiceCurrency", GetString(invoice, "currency")));
        header.Add(Element("DocumentFunctionCode", GetString(invoice, "documentFunctionCode")));
        return header;
    }

    private static XElement BuildParty(string name, JsonElement party)
    {
        var result = new XElement(name);
        AddIfPresent(result, "TaxID", GetString(party, "taxId"));
        AddIfPresent(result, "Name", GetString(party, "name"));
        AddIfPresent(result, "StreetAndNumber", GetString(party, "streetAndNumber"));
        AddIfPresent(result, "CityName", GetString(party, "city"));
        AddIfPresent(result, "PostalCode", GetString(party, "postalCode"));
        AddIfPresent(result, "Country", GetString(party, "country"));
        var phone = GetString(party, "phone");
        var email = GetString(party, "email");
        if (phone is not null || email is not null)
        {
            var contact = new XElement("ContactInformation");
            AddIfPresent(contact, "PhoneNumber", phone);
            AddIfPresent(contact, "ElectronicMail", email);
            result.Add(contact);
        }

        AddIfPresent(result, "VATPrefix", GetString(party, "vatPrefix"));
        return result;
    }

    private static XElement BuildLine(JsonElement line)
    {
        var item = new XElement("Line-Item",
            Element("LineNumber", GetInteger(line, "lineNumber")!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Element("ItemDescription", GetString(line, "description")));
        AddIfPresent(item, "InvoiceQuantity", GetString(line, "quantity"));
        AddIfPresent(item, "UnitOfMeasure", GetString(line, "unit"));
        AddIfPresent(item, "InvoiceUnitNetPrice", GetString(line, "unitNetPrice"));
        AddIfPresent(item, "TaxRate", GetString(line, "taxRate"));
        AddIfPresent(item, "TaxAmount", GetString(line, "taxAmount"));
        AddIfPresent(item, "NetAmount", GetString(line, "netAmount"));
        AddIfPresent(item, "GrossAmount", GetString(line, "grossAmount"));
        return new XElement("Line", item);
    }

    private static XElement BuildSummary(JsonElement summary)
    {
        var taxLines = GetArray(summary, "taxLines");
        var taxSummary = new XElement("Tax-Summary", taxLines.Select(taxLine =>
        {
            var result = new XElement("Tax-Summary-Line");
            AddIfPresent(result, "TaxRate", GetString(taxLine, "taxRate"));
            AddIfPresent(result, "TaxCategoryCode", GetString(taxLine, "taxCategoryCode"));
            result.Add(Element("TaxAmount", GetString(taxLine, "taxAmount")));
            result.Add(Element("TaxableAmount", GetString(taxLine, "taxableAmount")));
            return result;
        }));
        return new XElement("Invoice-Summary",
            Element("TotalLines", GetInteger(summary, "totalLines")!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Element("TotalGrossAmount", GetString(summary, "totalGrossAmount")),
            taxSummary);
    }

    private static List<string> MissingRequiredFacts(JsonElement invoice)
    {
        var missing = new List<string>();
        Require(invoice, "invoiceNumber", missing);
        Require(invoice, "invoiceDate", missing);
        Require(invoice, "currency", missing);
        Require(invoice, "documentFunctionCode", missing);
        var seller = GetObject(invoice, "seller");
        Require(seller, "taxId", missing, "seller.");
        Require(seller, "name", missing, "seller.");
        var buyer = GetObject(invoice, "buyer");
        Require(buyer, "taxId", missing, "buyer.");
        Require(buyer, "name", missing, "buyer.");
        var summary = GetObject(invoice, "summary");
        if (GetInteger(summary, "totalLines") is null) { missing.Add("summary.totalLines"); }
        Require(summary, "totalGrossAmount", missing, "summary.");
        var taxLines = GetArray(summary, "taxLines");
        if (taxLines.Count == 0) { missing.Add("summary.taxLines"); }
        for (var index = 0; index < taxLines.Count; index++)
        {
            Require(taxLines[index], "taxAmount", missing, $"summary.taxLines[{index}].");
            Require(taxLines[index], "taxableAmount", missing, $"summary.taxLines[{index}].");
        }
        return missing;
    }

    private static bool IsCompleteLine(JsonElement line) => GetInteger(line, "lineNumber") is not null && GetString(line, "description") is not null;

    private static void Require(JsonElement element, string property, ICollection<string> missing, string prefix = "")
    {
        if (GetString(element, property) is null) { missing.Add(prefix + property); }
    }

    private static XElement Element(string name, string? value) => new(name, value ?? throw new ArgumentException($"Comarch XML element {name} requires a value."));
    private static void AddIfPresent(XElement parent, string name, string? value)
    {
        if (value is not null) { parent.Add(new XElement(name, value)); }
    }

    private static List<JsonElement> GetArray(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().ToList()
            : [];

    private static JsonElement GetObject(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object ? property : default;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? GetInteger(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : null;

    private static ComarchXmlPreviewResult RenderLegacyTree(JsonElement extraction)
    {
        if (!extraction.TryGetProperty("comarchEcodKsef", out var invoice) || invoice.ValueKind != JsonValueKind.Object)
        {
            return new ComarchXmlPreviewResult(null, null, "The extraction does not contain normalized invoice facts. Restart processing to create them.");
        }

        var profileId = GetString(invoice, "profile");
        if (!ComarchInvoiceProfiles.IsSupported(profileId))
        {
            return new ComarchXmlPreviewResult(null, null, "The extraction contains an unsupported Comarch profile.");
        }

        if (!invoice.TryGetProperty("xml", out var xml) || xml.ValueKind != JsonValueKind.Object)
        {
            return new ComarchXmlPreviewResult(null, profileId, "The extraction does not contain a Comarch XML mapping. Restart processing to create normalized facts.");
        }

        try
        {
            var root = ToElement(xml);
            if (!string.Equals(root.Name.LocalName, "Document-Invoice", StringComparison.Ordinal))
            {
                return new ComarchXmlPreviewResult(null, profileId, "The full Comarch XML mapping must start with Document-Invoice.");
            }

            return new ComarchXmlPreviewResult(new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString(), profileId, null);
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
}
