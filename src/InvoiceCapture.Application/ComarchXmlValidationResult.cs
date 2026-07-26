namespace InvoiceCapture.Application;

public sealed record ComarchXmlValidationResult(bool IsValid, IReadOnlyList<string> Errors);
