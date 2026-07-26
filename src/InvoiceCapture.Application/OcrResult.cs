namespace InvoiceCapture.Application;

public sealed record OcrResult(string RawJson, string Markdown, IReadOnlyList<string> BlockIds);
