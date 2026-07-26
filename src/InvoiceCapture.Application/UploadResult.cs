using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed record UploadResult(DocumentId DocumentId, Guid JobId);
