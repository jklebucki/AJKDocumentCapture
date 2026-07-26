namespace InvoiceCapture.Domain;

public sealed record InvoiceLine(string Description, string? Unit, decimal Quantity, decimal NetAmount, decimal VatRate, decimal VatAmount, decimal GrossAmount, IReadOnlyList<SourceEvidence> Evidence);
