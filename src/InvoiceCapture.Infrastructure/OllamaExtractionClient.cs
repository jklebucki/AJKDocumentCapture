using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceCapture.Application;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public sealed class OllamaExtractionClient(HttpClient httpClient, IOptions<OllamaOptions> options) : IInvoiceExtractionClient
{
    private const string PromptVersion = "invoice-facts-comarch-ecod-v5";

    public ExtractionRequest PrepareRequest(OcrResult ocrResult)
    {
        var reduced = Reduce(ocrResult);
        var request = new
        {
            model = options.Value.Model,
            stream = false,
            think = "medium",
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
        "status": { "enum": ["ready", "needs_review"] },
        "documentType": { "enum": ["invoice", "receipt_with_nip", "correction", "unknown"] },
        "profile": { "enum": ["unknown", "comarch-ecod-invoice-7.77", "comarch-ecod-correction-7.77", "comarch-ecod-ksef-7.77", "comarch-ecod-ksef-correction-7.77"] },
        "invoice": { "$ref": "#/$defs/invoice" },
        "issues": { "type": "array", "items": { "$ref": "#/$defs/issue" } },
        "evidence": { "type": "array", "items": { "$ref": "#/$defs/evidence" } }
      },
      "required": ["status", "documentType", "profile", "invoice", "issues", "evidence"],
      "$defs": {
        "party": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "taxId": { "type": ["string", "null"] }, "vatPrefix": { "type": ["string", "null"] }, "name": { "type": ["string", "null"] },
            "streetAndNumber": { "type": ["string", "null"] }, "postalCode": { "type": ["string", "null"] }, "city": { "type": ["string", "null"] },
            "country": { "type": ["string", "null"] }, "email": { "type": ["string", "null"] }, "phone": { "type": ["string", "null"] }
          },
          "required": ["taxId", "vatPrefix", "name", "streetAndNumber", "postalCode", "city", "country", "email", "phone"]
        },
        "line": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "lineNumber": { "type": ["integer", "null"] }, "description": { "type": ["string", "null"] }, "quantity": { "type": ["string", "null"] },
            "unit": { "type": ["string", "null"] }, "unitNetPrice": { "type": ["string", "null"] }, "taxRate": { "type": ["string", "null"] },
            "netAmount": { "type": ["string", "null"] }, "taxAmount": { "type": ["string", "null"] }, "grossAmount": { "type": ["string", "null"] }
          },
          "required": ["lineNumber", "description", "quantity", "unit", "unitNetPrice", "taxRate", "netAmount", "taxAmount", "grossAmount"]
        },
        "taxLine": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "taxRate": { "type": ["string", "null"] }, "taxCategoryCode": { "type": ["string", "null"] }, "taxableAmount": { "type": ["string", "null"] },
            "taxAmount": { "type": ["string", "null"] }, "grossAmount": { "type": ["string", "null"] }
          },
          "required": ["taxRate", "taxCategoryCode", "taxableAmount", "taxAmount", "grossAmount"]
        },
        "summary": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "totalLines": { "type": ["integer", "null"] }, "totalNetAmount": { "type": ["string", "null"] }, "totalTaxAmount": { "type": ["string", "null"] },
            "totalGrossAmount": { "type": ["string", "null"] }, "taxLines": { "type": "array", "items": { "$ref": "#/$defs/taxLine" } }
          },
          "required": ["totalLines", "totalNetAmount", "totalTaxAmount", "totalGrossAmount", "taxLines"]
        },
        "invoice": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "invoiceNumber": { "type": ["string", "null"] }, "invoiceDate": { "type": ["string", "null"] }, "salesDate": { "type": ["string", "null"] },
            "invoicingPeriod": { "type": ["string", "null"] }, "currency": { "type": ["string", "null"] }, "ksefDocumentNumber": { "type": ["string", "null"] },
            "documentFunctionCode": { "enum": ["O", "C", "D", "R", null] }, "seller": { "$ref": "#/$defs/party" }, "buyer": { "$ref": "#/$defs/party" },
            "lines": { "type": "array", "items": { "$ref": "#/$defs/line" } }, "summary": { "$ref": "#/$defs/summary" }
          },
          "required": ["invoiceNumber", "invoiceDate", "salesDate", "invoicingPeriod", "currency", "ksefDocumentNumber", "documentFunctionCode", "seller", "buyer", "lines", "summary"]
        },
        "issue": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "severity": { "enum": ["warning", "error"] }, "code": { "type": "string" }, "path": { "type": "string" }, "message": { "type": "string" },
            "sourceBlockIds": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["severity", "code", "path", "message", "sourceBlockIds"]
        },
        "evidence": {
          "type": "object", "additionalProperties": false,
          "properties": { "path": { "type": "string" }, "quote": { "type": "string" }, "sourceBlockIds": { "type": "array", "items": { "type": "string" } } },
          "required": ["path", "quote", "sourceBlockIds"]
        }
      }
    }
    """);
}
