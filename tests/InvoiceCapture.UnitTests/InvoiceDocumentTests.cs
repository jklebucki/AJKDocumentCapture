using InvoiceCapture.Domain;

namespace InvoiceCapture.UnitTests;

public sealed class InvoiceDocumentTests
{
    [Fact]
    public void MoveTo_RejectsSkippedTransition()
    {
        var document = CreateDocument();

        var moved = document.MoveTo(ProcessingStatus.OcrRunning);

        Assert.False(moved);
        Assert.Equal(ProcessingStatus.Uploaded, document.Status);
    }

    [Fact]
    public void MoveTo_AcceptsNormalSequence()
    {
        var document = CreateDocument();

        Assert.True(document.MoveTo(ProcessingStatus.Queued));
        Assert.True(document.MoveTo(ProcessingStatus.Normalizing));
        Assert.Equal(ProcessingStatus.Normalizing, document.Status);
    }

    [Fact]
    public void RestartProcessing_Resets_review_required_document_for_a_fresh_queue_run()
    {
        var document = CreateDocument();
        document.MoveTo(ProcessingStatus.Queued);
        document.MoveTo(ProcessingStatus.Normalizing);
        document.MoveTo(ProcessingStatus.OcrRunning);
        document.MoveTo(ProcessingStatus.Extracting);
        document.MoveTo(ProcessingStatus.Validating);
        document.MoveTo(ProcessingStatus.ReviewRequired);
        document.ApplyExtraction(DocumentType.Invoice, new InvoiceParty("Seller", "123", null), new InvoiceParty("Buyer", "456", null), "FV/1", null, null, "EUR", null, null, [], [], null);
        document.SetValidationIssues([new ValidationIssue("missing_nip", ValidationSeverity.Error, "buyerNip", "Buyer NIP is missing.")]);

        document.RestartProcessing();

        Assert.Equal(ProcessingStatus.Queued, document.Status);
        Assert.Equal(DocumentType.Unknown, document.Type);
        Assert.Null(document.Buyer);
        Assert.Null(document.Seller);
        Assert.Empty(document.Issues);
        Assert.Equal("PLN", document.Currency);
    }

    [Fact]
    public void RestartProcessing_Rejects_an_active_document()
    {
        var document = CreateDocument();

        Assert.Throws<InvalidOperationException>(document.RestartProcessing);
    }

    private static InvoiceDocument CreateDocument()
    {
        var id = DocumentId.New();
        return new InvoiceDocument(id, new SourceDocument(id, "test.pdf", "application/pdf", "ABC", 10, "source/original.pdf"));
    }
}
