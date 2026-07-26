namespace InvoiceCapture.Domain;

public sealed class SourceDocument
{
    public SourceDocument(DocumentId id, string originalFileName, string mediaType, string sha256, long sizeBytes, string originalPath)
    {
        Id = id;
        OriginalFileName = originalFileName;
        MediaType = mediaType;
        Sha256 = sha256;
        SizeBytes = sizeBytes;
        OriginalPath = originalPath;
    }

    public DocumentId Id { get; }
    public string OriginalFileName { get; }
    public string MediaType { get; }
    public string Sha256 { get; }
    public long SizeBytes { get; }
    public string OriginalPath { get; }
}
