namespace InvoiceCapture.Infrastructure;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; init; } = "http://host.docker.internal:11434";
    public string Model { get; init; } = "gpt-oss:20b";
    public int TimeoutSeconds { get; init; } = 600;
}
