namespace InvoiceCapture.Domain;

public sealed class ProcessingJob
{
    public ProcessingJob(Guid id, DocumentId documentId, string idempotencyKey)
    {
        Id = id;
        DocumentId = documentId;
        IdempotencyKey = idempotencyKey;
        Status = ProcessingStatus.Queued;
        Stage = ProcessingStatus.Queued.ToString();
    }

    public static ProcessingJob Rehydrate(
        Guid id,
        DocumentId documentId,
        string idempotencyKey,
        ProcessingStatus status,
        string stage,
        int attempt,
        string? leaseOwner,
        DateTimeOffset? leaseUntil,
        string? errorCode)
    {
        var job = new ProcessingJob(id, documentId, idempotencyKey)
        {
            Status = status,
            Stage = stage,
            Attempt = attempt,
            LeaseOwner = leaseOwner,
            LeaseUntil = leaseUntil,
            ErrorCode = errorCode,
        };
        return job;
    }

    public Guid Id { get; }
    public DocumentId DocumentId { get; }
    public string IdempotencyKey { get; }
    public ProcessingStatus Status { get; private set; }
    public string Stage { get; private set; }
    public int Attempt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? HeartbeatAt { get; private set; }
    public string? ErrorCode { get; private set; }

    public void Acquire(string leaseOwner, DateTimeOffset leaseUntil)
    {
        LeaseOwner = leaseOwner;
        LeaseUntil = leaseUntil;
        HeartbeatAt = leaseUntil;
        Attempt++;
    }

    public void Advance(ProcessingStatus status) { Status = status; Stage = status.ToString(); }
    public void Fail(string errorCode) { Status = ProcessingStatus.Failed; Stage = ProcessingStatus.Failed.ToString(); ErrorCode = errorCode; }
}
