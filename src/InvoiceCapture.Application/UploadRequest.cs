namespace InvoiceCapture.Application;

public sealed record UploadRequest(string FileName, string ContentType, Stream Content, string IdempotencyKey);
