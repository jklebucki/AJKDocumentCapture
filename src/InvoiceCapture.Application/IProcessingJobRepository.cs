using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IProcessingJobRepository
{
    Task<Guid> EnqueueAsync(DocumentId documentId, string idempotencyKey, CancellationToken cancellationToken);
    Task<bool> RestartAsync(DocumentId documentId, CancellationToken cancellationToken);
    Task<ProcessingJob?> TryAcquireAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task CompleteStageAsync(Guid jobId, ProcessingStatus status, CancellationToken cancellationToken);
    Task FailAsync(Guid jobId, string errorCode, bool retryable, CancellationToken cancellationToken);
}
