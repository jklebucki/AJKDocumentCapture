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
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
