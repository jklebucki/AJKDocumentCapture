using InvoiceCapture.Application;
using InvoiceCapture.Domain;

namespace InvoiceCapture.UnitTests;

public sealed class LoadDocumentReviewHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_a_review_state_when_the_artifacts_directory_does_not_exist()
    {
        var id = DocumentId.New();
        var document = new InvoiceDocument(id, new SourceDocument(id, "invoice.pdf", "application/pdf", "hash", 10, "source/original.pdf"));
        var handler = new LoadDocumentReviewHandler(new ReviewInvoiceRepository(document), new MissingArtifactBlobStore());

        var result = await handler.HandleAsync(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.ExtractionXml);
        Assert.Contains("not available", result.ArtifactMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReviewInvoiceRepository(InvoiceDocument document) : IInvoiceRepository
    {
        public Task AddAsync(InvoiceDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountAsync(string? searchTerm, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<InvoiceDocument?> GetAsync(DocumentId documentId, CancellationToken cancellationToken) => Task.FromResult<InvoiceDocument?>(document);
        public Task<IReadOnlyList<DocumentSummary>> ListAsync(string? searchTerm, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(InvoiceDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MissingArtifactBlobStore : IBlobStore
    {
        public Task DeleteWorkDirectoryAsync(DocumentId documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken) => throw new DirectoryNotFoundException();
        public Task SaveArtifactAsync(DocumentId documentId, string relativePath, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StoredBlob> SaveOriginalAsync(DocumentId documentId, string extension, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
