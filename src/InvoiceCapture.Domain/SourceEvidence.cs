namespace InvoiceCapture.Domain;

public sealed record SourceEvidence(int Page, string BlockId, decimal X, decimal Y, decimal Width, decimal Height, string RawText);
