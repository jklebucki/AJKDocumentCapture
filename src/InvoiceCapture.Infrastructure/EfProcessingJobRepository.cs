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
        await db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task<ProcessingJob?> TryAcquireAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE processing_jobs SET "LeaseOwner" = @owner, "LeaseUntil" = NOW() + @lease, "Attempt" = "Attempt" + 1
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
        await db.SaveChangesAsync(cancellationToken);
    }
}
