namespace InvoiceCapture.Infrastructure;

internal sealed class ValidationIssueRow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string Code { get; set; }
    public required string Severity { get; set; }
    public required string Field { get; set; }
    public required string Message { get; set; }
}
