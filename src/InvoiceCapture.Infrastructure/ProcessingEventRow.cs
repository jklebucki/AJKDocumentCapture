namespace InvoiceCapture.Infrastructure;

internal sealed class ProcessingEventRow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid JobId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Kind { get; set; }
    public required string Stage { get; set; }
    public required string Detail { get; set; }
}
