using InvoiceCapture.Domain;
using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.UnitTests;

public sealed class FileSystemBlobStoreTests
{
    [Fact]
    public async Task SaveOriginalAsync_creates_missing_writable_storage_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"invoice-capture-test-{Guid.NewGuid():N}");
        var documentId = DocumentId.New();
        var store = new FileSystemBlobStore(Options.Create(new StorageOptions { Root = root }));
        await using var source = new MemoryStream([1, 2, 3]);

        try
        {
            var stored = await store.SaveOriginalAsync(documentId, ".png", source, CancellationToken.None);

            Assert.Equal(3, stored.Length);
            Assert.True(File.Exists(Path.Combine(root, stored.RelativePath)));
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
}
