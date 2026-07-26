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

    private static InvoiceDocument CreateDocument()
    {
        var id = DocumentId.New();
        return new InvoiceDocument(id, new SourceDocument(id, "test.pdf", "application/pdf", "ABC", 10, "source/original.pdf"));
    }
}
