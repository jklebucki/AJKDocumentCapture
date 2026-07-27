using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceCapture.Application;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public sealed class OllamaExtractionClient(HttpClient httpClient, IOptions<OllamaOptions> options) : IInvoiceExtractionClient
{
    private const string PromptVersion = "invoice-extraction-comarch-ecod-v4-compact";

    public ExtractionRequest PrepareRequest(OcrResult ocrResult)
    {
        var reduced = Reduce(ocrResult);
        var request = new
        {
            model = options.Value.Model,
            stream = false,
            options = new { temperature = 0 },
            format = InvoiceSchema.RootElement,
            messages = new[]
            {
                new { role = "system", content = OllamaExtractionPrompt.SystemInstructions },
                new { role = "user", content = $"{OllamaExtractionPrompt.UserTemplate}\n\nOCR input:\n{reduced}" }
            }
        };
        var requestJson = JsonSerializer.Serialize(request);
        return new ExtractionRequest(requestJson, PromptVersion, options.Value.Model, Hash(requestJson));
    }

    public async Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken)
    {
        using var requestContent = new StringContent(request.RequestJson, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("api/chat", requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var responseContent = body.RootElement.GetProperty("message").GetProperty("content").GetString() ?? throw new InvalidDataException("Ollama returned an empty response.");
        using var canonical = JsonDocument.Parse(responseContent);
        var normalized = JsonSerializer.Serialize(canonical.RootElement);
        return new ExtractionResult(normalized, request.PromptVersion, request.Model, request.RequestHash, Hash(normalized));
    }

    private static string Reduce(OcrResult result) => JsonSerializer.Serialize(new { markdown = result.Markdown, blockIds = result.BlockIds });
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static readonly JsonDocument InvoiceSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "documentType": { "type": "string" },
        "sourceBlockIds": { "type": "array", "items": { "type": "string" } },
        "comarchEcodKsef": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "profile": { "enum": ["comarch-ecod-invoice-7.77", "comarch-ecod-correction-7.77", "comarch-ecod-ksef-7.77", "comarch-ecod-ksef-correction-7.77"] },
            "xml": { "$ref": "#/$defs/xmlNode" }
          },
          "required": ["profile", "xml"]
        }
      },
      "required": ["documentType", "sourceBlockIds", "comarchEcodKsef"],
      "$defs": {
        "xmlNode": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "name": { "type": "string" },
            "value": { "type": ["string", "null"] },
            "children": { "type": "array", "items": { "$ref": "#/$defs/xmlNode" } }
          },
          "required": ["name", "value", "children"]
        }
      }
    }
    """);
}
