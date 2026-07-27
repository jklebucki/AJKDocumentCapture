using System.Net;
using System.Text;
using System.Text.Json;
using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.UnitTests;

public sealed class OllamaExtractionClientTests
{
    [Fact]
    public void PrepareRequest_places_strict_evidence_and_status_rules_at_the_start_of_the_system_prompt()
    {
        const string expectedPrefix = """
            A value is allowed in the output only when the exact source characters supporting it occur in OCR.

            Never construct an email address, phone number, tax identifier, document number, address or company name from context.

            For every non-null identifier, contact value, date and monetary value, an exact evidence quote is mandatory.

            If an evidence quote is not an exact substring of OCR, the extracted value must be null.

            Do not copy summary values into invoice lines.
            Do not copy line values into the tax summary.
            Do not infer unit price from quantity and net amount.
            Do not infer line gross amount from net and tax.

            If ksefDocumentNumber is non-null, profile must be one of the KSeF profiles.

            status must be needs_review when invoiceNumber, documentFunctionCode, seller tax ID or buyer tax ID is null.
            """;
        var client = CreateClient(new CapturingHandler());

        var request = client.PrepareRequest(new OcrResult("raw", "Invoice no. FV/1", ["block-7"]));

        using var body = JsonDocument.Parse(request.RequestJson);
        var systemPrompt = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.StartsWith(expectedPrefix, systemPrompt, StringComparison.Ordinal);
        Assert.Equal("invoice-facts-comarch-ecod-v6", request.PromptVersion);
    }

    [Fact]
    public async Task ExtractAsync_sends_the_exact_prepared_body_that_contains_the_document_ocr()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);
        var request = client.PrepareRequest(new OcrResult("raw", "Invoice no. FV/1\nNIP 5260250274", ["block-7"]));

        await client.ExtractAsync(request, CancellationToken.None);

        Assert.Equal(request.RequestJson, handler.Body);
        var body = Assert.IsType<string>(handler.Body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("gpt-oss:20b", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("medium", json.RootElement.GetProperty("think").GetString());
        Assert.True(json.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("invoice", out _));
        Assert.False(json.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("comarchEcodKsef", out _));
        Assert.Contains("Invoice no. FV/1", json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Contains("block-7", json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    private static OllamaExtractionClient CreateClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") },
            Options.Create(new OllamaOptions { Model = "gpt-oss:20b" }));

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string extraction = """{"documentType":"Invoice","sourceBlockIds":[],"comarchEcodKsef":{"profile":"comarch-ecod-invoice-7.77","xml":{"name":"Invoice","value":null,"children":[]}}}""";
            var response = JsonSerializer.Serialize(new { message = new { content = extraction } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
