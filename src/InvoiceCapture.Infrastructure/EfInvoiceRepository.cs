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
        if (row is null) { return null; }
        var document = Rehydrate(row);
        var issues = await db.ValidationIssues.AsNoTracking()
            .Where(x => x.DocumentId == documentId.Value)
            .OrderBy(x => x.Code)
            .Select(x => new ValidationIssue(x.Code, Enum.Parse<ValidationSeverity>(x.Severity), x.Field, x.Message))
            .ToListAsync(cancellationToken);
        document.SetValidationIssues(issues);
        return document;
    }

    public Task<int> CountAsync(string? searchTerm, CancellationToken cancellationToken) =>
        FilterBySearch(db.Invoices.AsNoTracking(), searchTerm).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(string? searchTerm, int skip, int take, CancellationToken cancellationToken)
    {
        var invoices = FilterBySearch(db.Invoices.AsNoTracking(), searchTerm);
        var rows = await (
            from invoice in invoices
            join job in db.Jobs.AsNoTracking() on invoice.Id equals job.DocumentId into jobs
            from job in jobs.DefaultIfEmpty()
            orderby invoice.CreatedAt descending
            select new
            {
                invoice.Id,
                invoice.OriginalFileName,
                invoice.Status,
                JobStatus = job == null ? null : job.Status,
                ProcessingStage = job == null ? null : job.Stage,
                invoice.CreatedAt,
                ProcessingStartedAt = job == null ? null : job.ProcessingStartedAt,
                invoice.BuyerNip,
                invoice.BuyerName,
                invoice.SellerNip,
                invoice.SellerName,
                ErrorCode = job == null ? null : job.ErrorCode
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new DocumentSummary(
            new DocumentId(x.Id),
            x.OriginalFileName,
            Enum.Parse<ProcessingStatus>(x.Status),
            x.JobStatus is null ? null : Enum.Parse<ProcessingStatus>(x.JobStatus),
            x.ProcessingStage,
            x.CreatedAt,
            x.ProcessingStartedAt,
            x.BuyerNip,
            x.BuyerName,
            x.SellerNip,
            x.SellerName,
            x.ErrorCode)).ToList();
    }

    public async Task UpdateAsync(InvoiceDocument document, CancellationToken cancellationToken)
    {
        var row = await db.Invoices.SingleAsync(x => x.Id == document.Id.Value, cancellationToken);
        row.Status = document.Status.ToString();
        row.BuyerNip = document.Buyer?.Nip;
        row.BuyerName = document.Buyer?.Name;
        row.SellerNip = document.Seller?.Nip;
        row.SellerName = document.Seller?.Name;
        var existingIssues = db.ValidationIssues.Where(x => x.DocumentId == document.Id.Value);
        db.ValidationIssues.RemoveRange(existingIssues);
        db.ValidationIssues.AddRange(document.Issues.Select(issue => new ValidationIssueRow
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id.Value,
            Code = issue.Code,
            Severity = issue.Severity.ToString(),
            Field = issue.Field,
            Message = issue.Message
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static InvoiceDocument Rehydrate(InvoiceRow row)
    {
        var id = new DocumentId(row.Id);
        var document = new InvoiceDocument(id, new SourceDocument(id, row.OriginalFileName, row.MediaType, row.Sha256, row.SizeBytes, row.OriginalPath));
        if (row.BuyerNip is not null || row.BuyerName is not null || row.SellerNip is not null || row.SellerName is not null)
        {
            document.ApplyExtraction(
                DocumentType.Unknown,
                new InvoiceParty(row.SellerName, row.SellerNip, null),
                new InvoiceParty(row.BuyerName, row.BuyerNip, null),
                null,
                null,
                null,
                "PLN",
                null,
                null,
                [],
                [],
                null);
        }
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

    private static IQueryable<InvoiceRow> FilterBySearch(IQueryable<InvoiceRow> invoices, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) { return invoices; }

        var term = searchTerm.Trim();
        return invoices.Where(x =>
            EF.Functions.ILike(x.OriginalFileName, $"%{term}%") ||
            EF.Functions.ILike(x.Status, $"%{term}%") ||
            (x.BuyerNip != null && EF.Functions.ILike(x.BuyerNip, $"%{term}%")) ||
            (x.BuyerName != null && EF.Functions.ILike(x.BuyerName, $"%{term}%")) ||
            (x.SellerNip != null && EF.Functions.ILike(x.SellerNip, $"%{term}%")) ||
            (x.SellerName != null && EF.Functions.ILike(x.SellerName, $"%{term}%")));
    }
}
