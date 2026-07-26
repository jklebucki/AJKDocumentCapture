using InvoiceCapture.Application;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public sealed class FileSystemWorkerHeartbeat(IOptions<StorageOptions> options) : IWorkerHeartbeat
{
    private const string RelativePath = "system/worker-heartbeat";
    private readonly string path = Path.Combine(Path.GetFullPath(options.Value.Root), RelativePath);

    public async Task BeatAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
    }

    public Task<DateTimeOffset?> GetLastSeenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTimeOffset?)null);
    }
}
