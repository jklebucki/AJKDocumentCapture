namespace InvoiceCapture.Web;

public sealed record DiagnosticsCheck(string Name, string Description, DiagnosticsStatus Status, string Detail, DateTimeOffset CheckedAt);
