using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.UnitTests;

public sealed class FileSystemWorkerHeartbeatTests
{
    [Fact]
    public async Task BeatAsync_records_a_current_heartbeat()
    {
        var root = Path.Combine(Path.GetTempPath(), $"invoice-capture-test-{Guid.NewGuid():N}");
        IWorkerHeartbeat heartbeat = new FileSystemWorkerHeartbeat(Options.Create(new StorageOptions { Root = root }));

        try
        {
            await heartbeat.BeatAsync(CancellationToken.None);

            var lastSeen = await heartbeat.GetLastSeenAsync(CancellationToken.None);
            Assert.NotNull(lastSeen);
            Assert.True(DateTimeOffset.UtcNow - lastSeen.Value < TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
}
