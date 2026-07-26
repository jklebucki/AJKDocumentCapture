using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed record DocumentSummary(DocumentId Id, string FileName, ProcessingStatus Status, DateTimeOffset CreatedAt, string? ErrorCode);
