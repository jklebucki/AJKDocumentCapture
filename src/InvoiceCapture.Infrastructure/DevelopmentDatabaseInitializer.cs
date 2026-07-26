using Microsoft.EntityFrameworkCore;

namespace InvoiceCapture.Infrastructure;

public static class DevelopmentDatabaseInitializer
{
    public static async Task EnsureCurrentSchemaAsync(InvoiceCaptureDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.EnsureCreatedAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE invoice_documents ADD COLUMN IF NOT EXISTS "BuyerNip" character varying(32);
            ALTER TABLE invoice_documents ADD COLUMN IF NOT EXISTS "BuyerName" character varying(512);
            ALTER TABLE invoice_documents ADD COLUMN IF NOT EXISTS "SellerNip" character varying(32);
            ALTER TABLE invoice_documents ADD COLUMN IF NOT EXISTS "SellerName" character varying(512);
            ALTER TABLE processing_jobs ADD COLUMN IF NOT EXISTS "ProcessingStartedAt" timestamp with time zone;
            CREATE TABLE IF NOT EXISTS processing_events (
                "Id" uuid PRIMARY KEY,
                "DocumentId" uuid NOT NULL,
                "JobId" uuid NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "Kind" character varying(64) NOT NULL,
                "Stage" character varying(64) NOT NULL,
                "Detail" character varying(512) NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_processing_events_DocumentId_OccurredAt" ON processing_events ("DocumentId", "OccurredAt");
            CREATE TABLE IF NOT EXISTS validation_issues (
                "Id" uuid PRIMARY KEY,
                "DocumentId" uuid NOT NULL,
                "Code" character varying(128) NOT NULL,
                "Severity" character varying(32) NOT NULL,
                "Field" character varying(128) NOT NULL,
                "Message" character varying(512) NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_validation_issues_DocumentId" ON validation_issues ("DocumentId");
            """,
            cancellationToken);
    }
}
