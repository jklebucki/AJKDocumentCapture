namespace InvoiceCapture.Application;

public interface IWorkerHeartbeat
{
    Task BeatAsync(CancellationToken cancellationToken);
    Task<DateTimeOffset?> GetLastSeenAsync(CancellationToken cancellationToken);
}
