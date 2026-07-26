namespace InvoiceCapture.Application;

public sealed record StoredBlob(string RelativePath, string Sha256, long Length);
