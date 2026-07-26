namespace InvoiceCapture.Application;

public interface IOcrClient
{
    Task<OcrResult> ExtractAsync(Stream source, string mediaType, CancellationToken cancellationToken);
}
