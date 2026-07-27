namespace InvoiceCapture.Application;

public sealed record ManualInvoiceUpdate(
    string? InvoiceNumber,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Currency,
    string? BuyerName,
    string? BuyerNip,
    string? SellerName,
    string? SellerNip,
    string? PaymentMethod,
    string? BankAccount,
    decimal? NetAmount,
    decimal? VatAmount,
    decimal? GrossAmount);
