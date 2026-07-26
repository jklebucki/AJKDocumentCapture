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
            """,
            cancellationToken);
    }
}
