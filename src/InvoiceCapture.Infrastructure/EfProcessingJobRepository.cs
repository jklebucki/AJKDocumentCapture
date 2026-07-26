using System.Data;
using InvoiceCapture.Application;
using InvoiceCapture.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InvoiceCapture.Infrastructure;

public sealed class EfProcessingJobRepository(InvoiceCaptureDbContext db) : IProcessingJobRepository
{
    public async Task<Guid> EnqueueAsync(DocumentId documentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) { return existing.Id; }
        var id = Guid.NewGuid();
        db.Jobs.Add(new JobRow { Id = id, DocumentId = documentId.Value, IdempotencyKey = idempotencyKey, NextAttemptAt = DateTimeOffset.UtcNow });
        AddEvent(documentId, id, "Queued", ProcessingStatus.Queued.ToString(), "Document accepted and queued for processing.");
        await db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task<bool> RestartAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == documentId.Value, cancellationToken);
        var job = await db.Jobs.SingleOrDefaultAsync(x => x.DocumentId == documentId.Value, cancellationToken);
        if (invoice is null || job is null || job.LeaseUntil >= DateTimeOffset.UtcNow ||
            !Enum.TryParse<ProcessingStatus>(invoice.Status, out var status) ||
            !Enum.TryParse<ProcessingStatus>(job.Status, out var jobStatus) ||
            (jobStatus is not ProcessingStatus.Failed && !InvoiceDocument.CanRestartProcessing(status)))
        {
            return false;
        }

        invoice.Status = ProcessingStatus.Queued.ToString();
        invoice.BuyerNip = null;
        invoice.BuyerName = null;
        invoice.SellerNip = null;
        invoice.SellerName = null;
        job.Status = ProcessingStatus.Queued.ToString();
        job.Stage = ProcessingStatus.Queued.ToString();
        job.Attempt = 0;
        job.LeaseOwner = null;
        job.LeaseUntil = null;
        job.ProcessingStartedAt = null;
        job.NextAttemptAt = DateTimeOffset.UtcNow;
        job.ErrorCode = null;
        AddEvent(documentId, job.Id, "Restarted", ProcessingStatus.Queued.ToString(), "Operator restarted processing from the queue.");
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ProcessingEvent>> ListEventsAsync(DocumentId documentId, CancellationToken cancellationToken) =>
        await db.Events.AsNoTracking()
            .Where(x => x.DocumentId == documentId.Value)
            .OrderBy(x => x.OccurredAt)
            .Select(x => new ProcessingEvent(x.OccurredAt, x.Kind, x.Stage, x.Detail))
            .ToListAsync(cancellationToken);

    public async Task RecordEventAsync(DocumentId documentId, Guid jobId, string kind, string stage, string detail, CancellationToken cancellationToken)
    {
        AddEvent(documentId, jobId, kind, stage, detail);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProcessingJob?> TryAcquireAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE processing_jobs SET "LeaseOwner" = @owner, "LeaseUntil" = NOW() + @lease, "Attempt" = "Attempt" + 1, "ProcessingStartedAt" = COALESCE("ProcessingStartedAt", NOW())
            WHERE "Id" = (SELECT "Id" FROM processing_jobs WHERE "Status" = 'Queued' AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW()) AND ("LeaseUntil" IS NULL OR "LeaseUntil" < NOW()) ORDER BY "NextAttemptAt" FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING "Id", "DocumentId", "IdempotencyKey", "Status", "Stage", "Attempt", "LeaseOwner", "LeaseUntil", "ErrorCode";
            """;
        command.Parameters.AddWithValue("owner", workerId);
        command.Parameters.AddWithValue("lease", leaseDuration);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) { return null; }
        return ProcessingJob.Rehydrate(
            reader.GetGuid(0),
            new DocumentId(reader.GetGuid(1)),
            reader.GetString(2),
            Enum.Parse<ProcessingStatus>(reader.GetString(3), ignoreCase: false),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    public async Task CompleteStageAsync(Guid jobId, ProcessingStatus status, CancellationToken cancellationToken)
    {
        var row = await db.Jobs.SingleAsync(x => x.Id == jobId, cancellationToken);
        row.Status = status is ProcessingStatus.Completed or ProcessingStatus.Ready or ProcessingStatus.ReviewRequired
            ? status.ToString()
            : ProcessingStatus.Queued.ToString();
        row.Stage = status.ToString();
        row.Attempt = 0;
        row.LeaseUntil = null;
        row.LeaseOwner = null;
        row.NextAttemptAt = DateTimeOffset.UtcNow;
        AddEvent(new DocumentId(row.DocumentId), row.Id, "Stage completed", status.ToString(), $"Processing advanced to {status}.");
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid jobId, string errorCode, bool retryable, CancellationToken cancellationToken)
    {
        var row = await db.Jobs.SingleAsync(x => x.Id == jobId, cancellationToken);
        row.ErrorCode = errorCode;
        row.LeaseUntil = null;
        row.LeaseOwner = null;
        row.Status = retryable && row.Attempt < 3 ? ProcessingStatus.Queued.ToString() : ProcessingStatus.Failed.ToString();
        row.NextAttemptAt = retryable ? DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, row.Attempt) + Random.Shared.NextDouble()) : null;
        var detail = retryable && row.Attempt < 3 ? $"Temporary provider error ({errorCode}); retry scheduled." : $"Processing stopped: {errorCode}.";
        AddEvent(new DocumentId(row.DocumentId), row.Id, retryable ? "Retry scheduled" : "Failed", row.Stage, detail);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void AddEvent(DocumentId documentId, Guid jobId, string kind, string stage, string detail) =>
        db.Events.Add(new ProcessingEventRow
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId.Value,
            JobId = jobId,
            OccurredAt = DateTimeOffset.UtcNow,
            Kind = kind,
            Stage = stage,
            Detail = detail
        });
}
