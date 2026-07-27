using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed class LoadOllamaRequestArtifactHandler(IBlobStore blobStore)
{
    public async Task<string?> HandleAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await blobStore.OpenReadAsync(Path.Combine(documentId.ToString(), "artifacts", "ollama-request.json"), cancellationToken);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
