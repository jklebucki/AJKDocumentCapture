namespace InvoiceCapture.Domain;

public enum ProcessingStatus
{
    Uploaded,
    Queued,
    Normalizing,
    OcrRunning,
    Extracting,
    Validating,
    ReviewRequired,
    Ready,
    Exporting,
    Completed,
    Failed
}
