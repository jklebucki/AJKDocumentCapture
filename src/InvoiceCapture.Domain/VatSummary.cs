namespace InvoiceCapture.Domain;

public sealed record VatSummary(decimal Rate, decimal NetAmount, decimal VatAmount, decimal GrossAmount);
