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

    [Fact]
    public async Task Ollama_request_handler_returns_the_exact_stored_request_without_transforming_it()
    {
        var id = DocumentId.New();
        var artifactId = Guid.NewGuid();
        var blobStore = new ExtractionArtifactBlobStore("{\"messages\":[{\"content\":\"OCR input\"}]}");
        var handler = new LoadOllamaRequestArtifactHandler(blobStore);

        var result = await handler.HandleAsync(id, artifactId, CancellationToken.None);

        Assert.Equal("{\"messages\":[{\"content\":\"OCR input\"}]}", result);
        Assert.Equal(Path.Combine(id.ToString(), "artifacts", "ollama-requests", $"{artifactId:N}.json"), blobStore.OpenedPath);
    }

    private sealed class ExtractionArtifactBlobStore(string content) : IBlobStore
    {
        public string? OpenedPath { get; private set; }
        public Task DeleteWorkDirectoryAsync(DocumentId documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            OpenedPath = relativePath;
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }
        public Task SaveArtifactAsync(DocumentId documentId, string relativePath, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StoredBlob> SaveOriginalAsync(DocumentId documentId, string extension, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
