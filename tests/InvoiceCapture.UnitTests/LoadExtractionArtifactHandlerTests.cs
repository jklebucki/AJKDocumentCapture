using System.Text;
using InvoiceCapture.Application;
using InvoiceCapture.Domain;

namespace InvoiceCapture.UnitTests;

public sealed class LoadExtractionArtifactHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_canonical_ollama_artifact_without_transforming_it()
    {
        var id = DocumentId.New();
        var handler = new LoadExtractionArtifactHandler(new ExtractionArtifactBlobStore("{\"answer\":42}"));

        var result = await handler.HandleAsync(id, CancellationToken.None);

        Assert.Equal("{\"answer\":42}", result);
    }

    private sealed class ExtractionArtifactBlobStore(string content) : IBlobStore
    {
        public Task DeleteWorkDirectoryAsync(DocumentId documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        public Task SaveArtifactAsync(DocumentId documentId, string relativePath, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StoredBlob> SaveOriginalAsync(DocumentId documentId, string extension, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
