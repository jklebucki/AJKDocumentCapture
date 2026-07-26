using System.Security.Cryptography;
using InvoiceCapture.Application;
using InvoiceCapture.Domain;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public sealed class FileSystemBlobStore(IOptions<StorageOptions> options) : IBlobStore
{
    private readonly string root = Path.GetFullPath(options.Value.Root);

    public async Task<StoredBlob> SaveOriginalAsync(DocumentId documentId, string extension, Stream content, CancellationToken cancellationToken)
    {
        var relativePath = Path.Combine(documentId.ToString(), "source", $"original{extension}");
        var fullPath = GetSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int count;
        while ((count = await content.ReadAsync(buffer, cancellationToken)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            hash.AppendData(buffer, 0, count);
            length += count;
        }

        return new StoredBlob(relativePath, Convert.ToHexString(hash.GetHashAndReset()), length);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(GetSafePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public async Task SaveArtifactAsync(DocumentId documentId, string relativePath, Stream content, CancellationToken cancellationToken)
    {
        var fullPath = GetSafePath(Path.Combine(documentId.ToString(), relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await content.CopyToAsync(destination, cancellationToken);
    }

    public Task DeleteWorkDirectoryAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workPath = GetSafePath(Path.Combine(documentId.ToString(), "work"));
        if (Directory.Exists(workPath)) { Directory.Delete(workPath, true); }
        return Task.CompletedTask;
    }

    private string GetSafePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid blob path.");
        }

        return path;
    }
}
