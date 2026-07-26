namespace InvoiceCapture.Application;

public interface IInvoiceExtractionClient
{
    Task<ExtractionResult> ExtractAsync(OcrResult ocrResult, CancellationToken cancellationToken);
}
