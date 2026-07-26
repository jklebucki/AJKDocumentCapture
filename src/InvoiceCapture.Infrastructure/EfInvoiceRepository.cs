using InvoiceCapture.Application;
using InvoiceCapture.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceCapture.Infrastructure;

public sealed class EfInvoiceRepository(InvoiceCaptureDbContext db) : IInvoiceRepository
{
    public async Task AddAsync(InvoiceDocument document, CancellationToken cancellationToken)
    {
        db.Invoices.Add(new InvoiceRow
        {
            Id = document.Id.Value,
            OriginalFileName = document.Source.OriginalFileName,
            MediaType = document.Source.MediaType,
            Sha256 = document.Source.Sha256,
            SizeBytes = document.Source.SizeBytes,
            OriginalPath = document.Source.OriginalPath,
            Status = document.Status.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvoiceDocument?> GetAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        var row = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId.Value, cancellationToken);
        return row is null ? null : Rehydrate(row);
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        return await db.Invoices.AsNoTracking().OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).Select(x => new DocumentSummary(new DocumentId(x.Id), x.OriginalFileName, Enum.Parse<ProcessingStatus>(x.Status), x.CreatedAt, null)).ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(InvoiceDocument document, CancellationToken cancellationToken)
    {
        var row = await db.Invoices.SingleAsync(x => x.Id == document.Id.Value, cancellationToken);
        row.Status = document.Status.ToString();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static InvoiceDocument Rehydrate(InvoiceRow row)
    {
        var id = new DocumentId(row.Id);
        var document = new InvoiceDocument(id, new SourceDocument(id, row.OriginalFileName, row.MediaType, row.Sha256, row.SizeBytes, row.OriginalPath));
        var target = Enum.Parse<ProcessingStatus>(row.Status);
        while (document.Status != target && document.Status != ProcessingStatus.Failed)
        {
            var next = document.Status switch
            {
                ProcessingStatus.Uploaded => ProcessingStatus.Queued,
                ProcessingStatus.Queued => ProcessingStatus.Normalizing,
                ProcessingStatus.Normalizing => ProcessingStatus.OcrRunning,
                ProcessingStatus.OcrRunning => ProcessingStatus.Extracting,
                ProcessingStatus.Extracting => ProcessingStatus.Validating,
                ProcessingStatus.Validating => ProcessingStatus.ReviewRequired,
                ProcessingStatus.ReviewRequired => ProcessingStatus.Ready,
                ProcessingStatus.Ready => ProcessingStatus.Exporting,
                ProcessingStatus.Exporting => ProcessingStatus.Completed,
                _ => throw new InvalidOperationException("Unsupported processing status.")
            };
            document.MoveTo(next);
        }

        return document;
    }
}
