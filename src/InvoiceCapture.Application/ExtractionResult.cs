namespace InvoiceCapture.Application;

public sealed record ExtractionResult(string CanonicalJson, string PromptVersion, string Model, string RequestHash, string ResponseHash);
