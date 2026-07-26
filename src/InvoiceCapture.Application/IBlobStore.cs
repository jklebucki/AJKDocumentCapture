using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IBlobStore
{
    Task<StoredBlob> SaveOriginalAsync(DocumentId documentId, string extension, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
    Task SaveArtifactAsync(DocumentId documentId, string relativePath, Stream content, CancellationToken cancellationToken);
    Task DeleteWorkDirectoryAsync(DocumentId documentId, CancellationToken cancellationToken);
}
