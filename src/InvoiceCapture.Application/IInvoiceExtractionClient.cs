namespace InvoiceCapture.Application;

public interface IInvoiceExtractionClient
{
    ExtractionRequest PrepareRequest(OcrResult ocrResult);
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken);
}
