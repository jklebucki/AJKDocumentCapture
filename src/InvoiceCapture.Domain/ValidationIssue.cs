namespace InvoiceCapture.Domain;

public sealed record ValidationIssue(string Code, ValidationSeverity Severity, string Field, string Message);
