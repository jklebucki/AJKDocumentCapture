using System.Text.Json;
using System.Xml.Linq;

namespace InvoiceCapture.Application;

public static class ComarchEcodKsefXmlPreviewRenderer
{
    private const string Profile = "comarch-ecod-ksef-7.77";

    public static ComarchXmlPreviewResult Render(JsonElement extraction)
    {
        if (!extraction.TryGetProperty("comarchEcodKsef", out var invoice) || invoice.ValueKind != JsonValueKind.Object)
        {
            return new ComarchXmlPreviewResult(null, "This older extraction does not contain the Comarch ECOD/KSeF 7.77 profile. Restart processing to create it.");
        }

        if (!string.Equals(GetString(invoice, "profile"), Profile, StringComparison.Ordinal))
        {
            return new ComarchXmlPreviewResult(null, "The extraction contains an unsupported Comarch profile.");
        }

        var header = GetObject(invoice, "header");
        var buyer = GetObject(invoice, "buyer");
        var seller = GetObject(invoice, "seller");
        var summary = GetObject(invoice, "summary");
        var missing = RequiredMissing(header, buyer, seller, invoice, summary);
        if (missing.Count > 0)
        {
            return new ComarchXmlPreviewResult(null, $"ECOD/KSeF preflight is incomplete: {string.Join(", ", missing)}. Complete these fields during review before generating an ERP XML.");
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Document-Invoice",
                Header(header),
                new XElement("Invoice-Parties", Party("Buyer", buyer), Party("Seller", seller)),
                Lines(invoice),
                Summary(summary)));

        return new ComarchXmlPreviewResult(document.ToString(), null);
    }

    private static IReadOnlyList<string> RequiredMissing(JsonElement header, JsonElement buyer, JsonElement seller, JsonElement invoice, JsonElement summary)
    {
        var missing = new List<string>();
        Require(header, "invoiceNumber", "header.invoiceNumber", missing);
        Require(header, "invoiceDate", "header.invoiceDate", missing);
        Require(header, "salesDate", "header.salesDate", missing);
        Require(header, "invoiceCurrency", "header.invoiceCurrency", missing);
        Require(header, "documentFunctionCode", "header.documentFunctionCode", missing);
        Require(buyer, "taxId", "buyer.taxId", missing);
        Require(seller, "taxId", "seller.taxId", missing);
        if (!invoice.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0)
        {
            missing.Add("lines");
        }
        else
        {
            foreach (var line in lines.EnumerateArray())
            {
                Require(line, "lineNumber", "lines[].lineNumber", missing);
                Require(line, "itemDescription", "lines[].itemDescription", missing);
            }
        }

        Require(summary, "totalLines", "summary.totalLines", missing);
        Require(summary, "totalGrossAmount", "summary.totalGrossAmount", missing);
        if (!summary.TryGetProperty("taxSummary", out var taxSummary) || taxSummary.ValueKind != JsonValueKind.Array || taxSummary.GetArrayLength() == 0)
        {
            missing.Add("summary.taxSummary");
        }

        return missing.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static XElement Header(JsonElement header) => new("Invoice-Header",
        Element("InvoiceNumber", header, "invoiceNumber"),
        Element("InvoiceDate", header, "invoiceDate"),
        Element("SalesDate", header, "salesDate"),
        Element("InvoiceCurrency", header, "invoiceCurrency"),
        Element("InvoicePaymentDueDate", header, "invoicePaymentDueDate"),
        Element("InvoicePaymentMeans", header, "invoicePaymentMeans"),
        Element("DocumentFunctionCode", header, "documentFunctionCode"),
        Element("MessageType", header, "messageType"));

    private static XElement Party(string name, JsonElement party) => new(name,
        Element("TaxID", party, "taxId"),
        Element("Name", party, "name"),
        Element("StreetAndNumber", party, "streetAndNumber"),
        Element("CityName", party, "cityName"),
        Element("PostalCode", party, "postalCode"),
        Element("Country", party, "country"),
        Element("VATPrefix", party, "vatPrefix"),
        Element("AccountNumber", party, "accountNumber"));

    private static XElement Lines(JsonElement invoice)
    {
        var result = new XElement("Invoice-Lines");
        if (!invoice.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) { return result; }
        foreach (var line in lines.EnumerateArray())
        {
            result.Add(new XElement("Line", new XElement("Line-Item",
                Element("LineNumber", line, "lineNumber"),
                Element("ItemDescription", line, "itemDescription"),
                Element("InvoiceQuantity", line, "invoiceQuantity"),
                Element("UnitOfMeasure", line, "unitOfMeasure"),
                Element("InvoiceUnitNetPrice", line, "invoiceUnitNetPrice"),
                Element("TaxRate", line, "taxRate"),
                Element("VATRate", line, "vatRate"),
                Element("TaxCategoryCode", line, "taxCategoryCode"),
                Element("TaxAmount", line, "taxAmount"),
                Element("NetAmount", line, "netAmount"),
                Element("GrossAmount", line, "grossAmount"))));
        }

        return result;
    }

    private static XElement Summary(JsonElement summary)
    {
        var result = new XElement("Invoice-Summary",
            Element("TotalLines", summary, "totalLines"),
            Element("TotalNetAmount", summary, "totalNetAmount"),
            Element("TotalTaxAmount", summary, "totalTaxAmount"),
            Element("TotalGrossAmount", summary, "totalGrossAmount"));
        var taxSummary = new XElement("Tax-Summary");
        if (summary.TryGetProperty("taxSummary", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                taxSummary.Add(new XElement("Tax-Summary-Line",
                    Element("TaxRate", entry, "taxRate"),
                    Element("TaxCategoryCode", entry, "taxCategoryCode"),
                    Element("TaxAmount", entry, "taxAmount"),
                    Element("TaxableAmount", entry, "taxableAmount"),
                    Element("GrossAmount", entry, "grossAmount")));
            }
        }

        result.Add(taxSummary);
        return result;
    }

    private static void Require(JsonElement element, string propertyName, string field, ICollection<string> missing)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(property.ToString()))
        {
            missing.Add(field);
        }
    }

    private static XElement? Element(string name, JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null) { return null; }
        return new XElement(name, property.ToString());
    }

    private static JsonElement GetObject(JsonElement element, string propertyName) => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object ? property : default;

    private static string? GetString(JsonElement element, string propertyName) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : null;
}
