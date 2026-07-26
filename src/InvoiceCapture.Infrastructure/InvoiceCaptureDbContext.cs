using Microsoft.EntityFrameworkCore;

namespace InvoiceCapture.Infrastructure;

public sealed class InvoiceCaptureDbContext(DbContextOptions<InvoiceCaptureDbContext> options) : DbContext(options)
{
    internal DbSet<InvoiceRow> Invoices => Set<InvoiceRow>();
    internal DbSet<JobRow> Jobs => Set<JobRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var invoices = modelBuilder.Entity<InvoiceRow>();
        invoices.ToTable("invoice_documents");
        invoices.HasKey(x => x.Id);
        invoices.Property(x => x.OriginalFileName).HasMaxLength(255);
        invoices.Property(x => x.MediaType).HasMaxLength(128);
        invoices.Property(x => x.Sha256).HasMaxLength(64);
        invoices.Property(x => x.OriginalPath).HasMaxLength(512);
        invoices.Property(x => x.Status).HasMaxLength(32);
        invoices.Property(x => x.BuyerNip).HasMaxLength(32);
        invoices.Property(x => x.SellerNip).HasMaxLength(32);
        invoices.Property(x => x.BuyerName).HasMaxLength(512);
        invoices.Property(x => x.SellerName).HasMaxLength(512);
        invoices.Property(x => x.Version).IsRowVersion();
        invoices.HasIndex(x => x.Sha256);
        invoices.HasIndex(x => new { x.Status, x.CreatedAt });

        var jobs = modelBuilder.Entity<JobRow>();
        jobs.ToTable("processing_jobs");
        jobs.HasKey(x => x.Id);
        jobs.Property(x => x.IdempotencyKey).HasMaxLength(128);
        jobs.Property(x => x.Status).HasMaxLength(32);
        jobs.Property(x => x.Stage).HasMaxLength(32);
        jobs.Property(x => x.LeaseOwner).HasMaxLength(128);
        jobs.Property(x => x.ErrorCode).HasMaxLength(128);
        jobs.Property(x => x.Version).IsRowVersion();
        jobs.HasIndex(x => x.IdempotencyKey).IsUnique();
        jobs.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
