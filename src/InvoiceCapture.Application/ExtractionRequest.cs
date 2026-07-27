namespace InvoiceCapture.Application;

public sealed record ExtractionRequest(string RequestJson, string PromptVersion, string Model, string RequestHash);
