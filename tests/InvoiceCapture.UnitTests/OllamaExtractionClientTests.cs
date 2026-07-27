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
    public async Task ExtractAsync_sends_the_exact_prepared_body_that_contains_the_document_ocr()
    {
        var handler = new CapturingHandler();
        var client = new OllamaExtractionClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") },
            Options.Create(new OllamaOptions { Model = "gpt-oss:20b" }));
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
