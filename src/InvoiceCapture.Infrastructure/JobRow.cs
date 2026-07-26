namespace InvoiceCapture.Infrastructure;

internal sealed class JobRow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string IdempotencyKey { get; set; }
    public string Status { get; set; } = "Queued";
    public string Stage { get; set; } = "Queued";
    public int Attempt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? ErrorCode { get; set; }
    public uint Version { get; set; }
}
