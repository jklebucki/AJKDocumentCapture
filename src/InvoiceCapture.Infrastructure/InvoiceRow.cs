namespace InvoiceCapture.Infrastructure;

internal sealed class InvoiceRow
{
    public Guid Id { get; set; }
    public required string OriginalFileName { get; set; }
    public required string MediaType { get; set; }
    public required string Sha256 { get; set; }
    public long SizeBytes { get; set; }
    public required string OriginalPath { get; set; }
    public string Status { get; set; } = "Uploaded";
    public string Type { get; set; } = "Unknown";
    public DateTimeOffset CreatedAt { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Currency { get; set; } = "PLN";
    public string? PaymentMethod { get; set; }
    public string? BankAccount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? GrossAmount { get; set; }
    public string? BuyerNip { get; set; }
    public string? BuyerName { get; set; }
    public string? SellerNip { get; set; }
    public string? SellerName { get; set; }
    public uint Version { get; set; }
}
