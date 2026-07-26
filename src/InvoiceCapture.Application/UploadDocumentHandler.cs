using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed class UploadDocumentHandler(IBlobStore blobStore, IInvoiceRepository invoiceRepository, IProcessingJobRepository jobRepository)
{
    private const long MaximumSize = 25L * 1024 * 1024;

    public async Task<UploadResult> HandleAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(request));
        }

        var documentId = DocumentId.New();
        var extension = FileTypePolicy.Validate(request.FileName, request.ContentType);
        await using var limited = new SizeLimitedReadStream(request.Content, MaximumSize);
        var stored = await blobStore.SaveOriginalAsync(documentId, extension, limited, cancellationToken);
        var source = new SourceDocument(documentId, Path.GetFileName(request.FileName), request.ContentType, stored.Sha256, stored.Length, stored.RelativePath);
        await invoiceRepository.AddAsync(new InvoiceDocument(documentId, source), cancellationToken);
        var jobId = await jobRepository.EnqueueAsync(documentId, request.IdempotencyKey, cancellationToken);
        return new UploadResult(documentId, jobId);
    }
}
