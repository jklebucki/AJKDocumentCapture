using System.Text.Json;
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
            var preview = ComarchEcodKsefXmlPreviewRenderer.Render(parsed.RootElement);
            return new DocumentReviewResult(document, preview.Xml, preview.Message);
        }
        catch (FileNotFoundException)
        {
            return new DocumentReviewResult(document, null, "The extraction artifact is not available yet. Restart processing after resolving any provider issue.");
        }
        catch (DirectoryNotFoundException)
        {
            return new DocumentReviewResult(document, null, "The extraction artifact is not available yet. Restart processing after resolving any provider issue.");
        }
        catch (JsonException)
        {
            return new DocumentReviewResult(document, null, "The extraction artifact is invalid and cannot be reviewed.");
        }
    }

}
