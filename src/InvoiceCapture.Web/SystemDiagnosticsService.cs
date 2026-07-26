using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Web;

public sealed class SystemDiagnosticsService(InvoiceCaptureDbContext database, IOptions<StorageOptions> storage, IOptions<PaddleOcrOptions> paddle, IOptions<OllamaOptions> ollama, IWorkerHeartbeat workerHeartbeat, IHttpClientFactory httpClientFactory)
{
    public async Task<IReadOnlyList<DiagnosticsCheck>> CheckAsync(CancellationToken cancellationToken)
    {
        return [
            await CheckDatabaseAsync(cancellationToken),
            await CheckStorageAsync(cancellationToken),
            await CheckWorkerAsync(cancellationToken),
            await CheckPaddleAsync(cancellationToken),
            await CheckOllamaAsync(cancellationToken)
        ];
    }

    private async Task<DiagnosticsCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connected = await database.Database.CanConnectAsync(cancellationToken);
            return Create("PostgreSQL", "Durable queue and document metadata", connected ? DiagnosticsStatus.Healthy : DiagnosticsStatus.Unavailable, connected ? "Connection accepted." : "Connection was rejected.");
        }
        catch (Exception)
        {
            return Create("PostgreSQL", "Durable queue and document metadata", DiagnosticsStatus.Unavailable, "Connection failed.");
        }
    }

    private async Task<DiagnosticsCheck> CheckStorageAsync(CancellationToken cancellationToken)
    {
        var root = storage.Value.Root;
        var probe = Path.Combine(root, $".diagnostics-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            await using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await stream.FlushAsync(cancellationToken);
            return Create("Document storage", "Original files and processing artifacts", DiagnosticsStatus.Healthy, "Writable storage is available.");
        }
        catch (Exception)
        {
            return Create("Document storage", "Original files and processing artifacts", DiagnosticsStatus.Unavailable, "Storage is not writable.");
        }
    }

    private async Task<DiagnosticsCheck> CheckWorkerAsync(CancellationToken cancellationToken)
    {
        var lastSeen = await workerHeartbeat.GetLastSeenAsync(cancellationToken);
        if (lastSeen is null) { return Create("Processing worker", "Consumes queued document jobs", DiagnosticsStatus.Unavailable, "No heartbeat has been recorded."); }

        var age = DateTimeOffset.UtcNow - lastSeen.Value;
        return Create("Processing worker", "Consumes queued document jobs", age <= TimeSpan.FromSeconds(30) ? DiagnosticsStatus.Healthy : DiagnosticsStatus.Warning, age <= TimeSpan.FromSeconds(30) ? "Heartbeat is current." : $"Last heartbeat {age.TotalSeconds:0} seconds ago.");
    }

    private async Task<DiagnosticsCheck> CheckPaddleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await CreateProviderClient(paddle.Value.BaseUrl).GetAsync("health", cancellationToken);
            return Create("PaddleOCR-VL", "OCR pipeline for PDF and images", response.IsSuccessStatusCode ? DiagnosticsStatus.Healthy : DiagnosticsStatus.Unavailable, $"HTTP {(int)response.StatusCode}.");
        }
        catch (Exception)
        {
            return Create("PaddleOCR-VL", "OCR pipeline for PDF and images", DiagnosticsStatus.Unavailable, "Endpoint is unavailable.");
        }
    }

    private async Task<DiagnosticsCheck> CheckOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await CreateProviderClient(ollama.Value.BaseUrl).GetFromJsonAsync<OllamaTags>("api/tags", cancellationToken);
            var available = response?.Models?.Any(x => string.Equals(x.Name, ollama.Value.Model, StringComparison.Ordinal)) == true;
            return Create("Ollama", "Structured extraction with the configured model", available ? DiagnosticsStatus.Healthy : DiagnosticsStatus.Warning, available ? "Configured model is available." : "Configured model is not available.");
        }
        catch (Exception)
        {
            return Create("Ollama", "Structured extraction with the configured model", DiagnosticsStatus.Unavailable, "Endpoint is unavailable.");
        }
    }

    private HttpClient CreateProviderClient(string baseUrl)
    {
        var client = httpClientFactory.CreateClient("diagnostics");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        return client;
    }

    private static DiagnosticsCheck Create(string name, string description, DiagnosticsStatus status, string detail) => new(name, description, status, detail, DateTimeOffset.UtcNow);

    private sealed record OllamaTags(IReadOnlyList<OllamaModel>? Models);
    private sealed record OllamaModel(string Name);
}
