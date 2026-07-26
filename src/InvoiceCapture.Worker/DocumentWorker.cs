using InvoiceCapture.Application;

namespace InvoiceCapture.Worker;

public sealed class DocumentWorker(IServiceScopeFactory scopeFactory, ILogger<DocumentWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private DateTimeOffset lastHeartbeatAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                if (DateTimeOffset.UtcNow - lastHeartbeatAt > TimeSpan.FromSeconds(10))
                {
                    var heartbeat = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeat>();
                    await heartbeat.BeatAsync(stoppingToken);
                    lastHeartbeatAt = DateTimeOffset.UtcNow;
                }
                var repository = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
                var job = await repository.TryAcquireAsync(workerId, TimeSpan.FromMinutes(5), stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
                await processor.ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Document worker loop failed without document data.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
