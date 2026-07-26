using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed record DocumentSummary(
    DocumentId Id,
    string FileName,
    ProcessingStatus Status,
    string? ProcessingStage,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessingStartedAt,
    string? BuyerNip,
    string? BuyerName,
    string? SellerNip,
    string? SellerName,
    string? ErrorCode);
