using System.Text.Json;
using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed class LoadDocumentReviewHandler(IInvoiceRepository invoices, IBlobStore blobStore, IComarchInvoiceXmlValidator comarchValidator)
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
            if (preview.Xml is null || preview.ProfileId is null)
            {
                return new DocumentReviewResult(document, null, preview.Message);
            }

            var validation = await comarchValidator.ValidateAsync(preview.ProfileId, preview.Xml, cancellationToken);
            var message = validation.IsValid
                ? null
                : $"Comarch ECOD/KSeF XSD validation failed: {string.Join(" ", validation.Errors)}";
            return new DocumentReviewResult(document, preview.Xml, message);
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
