using InvoiceCapture.Application;
using InvoiceCapture.Domain;
using InvoiceCapture.Infrastructure;

namespace InvoiceCapture.UnitTests;

public sealed class UpdateManualInvoiceHandlerTests
{
    [Fact]
    public async Task HandleAsync_persists_manual_values_and_releases_a_valid_review_document()
    {
        var id = DocumentId.New();
        var document = new InvoiceDocument(id, new SourceDocument(id, "invoice.pdf", "application/pdf", "hash", 1, "source/original.pdf"));
        MoveToReviewRequired(document);
        var repository = new ManualInvoiceRepository(document);
        var handler = new UpdateManualInvoiceHandler(repository, new InvoiceValidator());

        var result = await handler.HandleAsync(id, new ManualInvoiceUpdate(" FV/1 ", new DateOnly(2026, 7, 27), null, "pln", "Buyer", "5260250274", "Seller", "5260250274", "42", null, 0m, 0m, 0m), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(repository.WasUpdated);
        Assert.Equal("FV/1", result.InvoiceNumber);
        Assert.Equal("PLN", result.Currency);
        Assert.Equal(ProcessingStatus.Ready, result.Status);
        Assert.DoesNotContain(result.Issues, x => x.Severity == ValidationSeverity.Error);
    }

    private static void MoveToReviewRequired(InvoiceDocument document)
    {
        Assert.True(document.MoveTo(ProcessingStatus.Queued));
        Assert.True(document.MoveTo(ProcessingStatus.Normalizing));
        Assert.True(document.MoveTo(ProcessingStatus.OcrRunning));
        Assert.True(document.MoveTo(ProcessingStatus.Extracting));
        Assert.True(document.MoveTo(ProcessingStatus.Validating));
        Assert.True(document.MoveTo(ProcessingStatus.ReviewRequired));
    }

    private sealed class ManualInvoiceRepository(InvoiceDocument document) : IInvoiceRepository
    {
        public bool WasUpdated { get; private set; }
        public Task AddAsync(InvoiceDocument document, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountAsync(string? searchTerm, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<InvoiceDocument?> GetAsync(DocumentId documentId, CancellationToken cancellationToken) => Task.FromResult<InvoiceDocument?>(document);
        public Task<IReadOnlyList<DocumentSummary>> ListAsync(string? searchTerm, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(InvoiceDocument document, CancellationToken cancellationToken)
        {
            WasUpdated = true;
            return Task.CompletedTask;
        }
    }
}
