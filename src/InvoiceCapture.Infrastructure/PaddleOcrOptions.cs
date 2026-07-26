namespace InvoiceCapture.Infrastructure;

public sealed class PaddleOcrOptions
{
    public const string SectionName = "PaddleOcr";
    public string BaseUrl { get; init; } = "http://paddleocr-vl-api:8080";
    public int TimeoutSeconds { get; init; } = 600;
}
