using System.Net.Http.Json;
using System.Text.Json;
using InvoiceCapture.Application;

namespace InvoiceCapture.Infrastructure;

public sealed class PaddleOcrClient(HttpClient httpClient) : IOcrClient
{
    public async Task<OcrResult> ExtractAsync(Stream source, string mediaType, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        var payload = new
        {
            file = Convert.ToBase64String(buffer.GetBuffer(), 0, checked((int)buffer.Length)),
            fileType = mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ? 0 : 1
        };
        using var response = await httpClient.PostAsJsonAsync("layout-parsing", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        var markdown = FindMarkdown(document.RootElement);
        return new OcrResult(raw, markdown, []);
    }

    private static string FindMarkdown(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("markdown") && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString() ?? string.Empty;
                }
                if (property.NameEquals("markdown") && property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("text", out var markdownText) && markdownText.ValueKind == JsonValueKind.String)
                {
                    return markdownText.GetString() ?? string.Empty;
                }

                var nested = FindMarkdown(property.Value);
                if (!string.IsNullOrEmpty(nested)) { return nested; }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindMarkdown(item);
                if (!string.IsNullOrEmpty(nested)) { return nested; }
            }
        }

        return string.Empty;
    }
}
