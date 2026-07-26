using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed class LoadDocumentReviewHandler(IInvoiceRepository invoices, IBlobStore blobStore)
{
    public async Task<DocumentReviewResult?> HandleAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        var document = await invoices.GetAsync(documentId, cancellationToken);
        if (document is null) { return null; }

        try
        {
            await using var stream = await blobStore.OpenReadAsync(Path.Combine(documentId.ToString(), "artifacts", "extraction.json"), cancellationToken);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken);
            using var parsed = JsonDocument.Parse(json);
            return new DocumentReviewResult(document, new XDocument(ToElement(parsed.RootElement, "extraction")).ToString(), null);
        }
        catch (FileNotFoundException)
        {
            return new DocumentReviewResult(document, null, "The extraction artifact is not available yet. Restart processing after resolving any provider issue.");
        }
        catch (JsonException)
        {
            return new DocumentReviewResult(document, null, "The extraction artifact is invalid and cannot be reviewed.");
        }
    }

    private static XElement ToElement(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Object => new XElement(name, element.EnumerateObject().Select(property => ToElement(property.Value, XmlConvert.EncodeLocalName(property.Name)))),
        JsonValueKind.Array => new XElement(name, element.EnumerateArray().Select(item => ToElement(item, "item"))),
        JsonValueKind.Null => new XElement(name),
        _ => new XElement(name, element.ToString())
    };
}
