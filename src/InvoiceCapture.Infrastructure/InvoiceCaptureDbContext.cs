using Microsoft.EntityFrameworkCore;

namespace InvoiceCapture.Infrastructure;

public sealed class InvoiceCaptureDbContext(DbContextOptions<InvoiceCaptureDbContext> options) : DbContext(options)
{
    internal DbSet<InvoiceRow> Invoices => Set<InvoiceRow>();
    internal DbSet<JobRow> Jobs => Set<JobRow>();
    internal DbSet<ProcessingEventRow> Events => Set<ProcessingEventRow>();
    internal DbSet<ValidationIssueRow> ValidationIssues => Set<ValidationIssueRow>();

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
        invoices.Property(x => x.Type).HasMaxLength(32);
        invoices.Property(x => x.InvoiceNumber).HasMaxLength(256);
        invoices.Property(x => x.Currency).HasMaxLength(3);
        invoices.Property(x => x.PaymentMethod).HasMaxLength(256);
        invoices.Property(x => x.BankAccount).HasMaxLength(34);
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

        var events = modelBuilder.Entity<ProcessingEventRow>();
        events.ToTable("processing_events");
        events.HasKey(x => x.Id);
        events.Property(x => x.Kind).HasMaxLength(64);
        events.Property(x => x.Stage).HasMaxLength(64);
        events.Property(x => x.Detail).HasMaxLength(512);
        events.HasIndex(x => new { x.DocumentId, x.OccurredAt });

        var issues = modelBuilder.Entity<ValidationIssueRow>();
        issues.ToTable("validation_issues");
        issues.HasKey(x => x.Id);
        issues.Property(x => x.Code).HasMaxLength(128);
        issues.Property(x => x.Severity).HasMaxLength(32);
        issues.Property(x => x.Field).HasMaxLength(128);
        issues.Property(x => x.Message).HasMaxLength(512);
        issues.HasIndex(x => x.DocumentId);
    }
}
