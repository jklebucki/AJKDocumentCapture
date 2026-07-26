namespace InvoiceCapture.Domain;

public sealed record InvoiceTotals(decimal NetAmount, decimal VatAmount, decimal GrossAmount);
