using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IProcessingJobRepository
{
    Task<Guid> EnqueueAsync(DocumentId documentId, string idempotencyKey, CancellationToken cancellationToken);
    Task<bool> RestartAsync(DocumentId documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessingEvent>> ListEventsAsync(DocumentId documentId, CancellationToken cancellationToken);
    Task RecordEventAsync(DocumentId documentId, Guid jobId, string kind, string stage, string detail, CancellationToken cancellationToken);
    Task<ProcessingJob?> TryAcquireAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task CompleteStageAsync(Guid jobId, ProcessingStatus status, CancellationToken cancellationToken);
    Task FailAsync(Guid jobId, string errorCode, bool retryable, CancellationToken cancellationToken);
}
