namespace InvoiceCapture.Application;

public sealed record XmlExportResult(string RelativePath, string Sha256, string SchemaVersion);
