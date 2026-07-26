namespace InvoiceCapture.Domain;

public sealed class InvoiceDocument
{
    private static readonly IReadOnlyDictionary<ProcessingStatus, ProcessingStatus[]> Transitions =
        new Dictionary<ProcessingStatus, ProcessingStatus[]>
        {
            [ProcessingStatus.Uploaded] = [ProcessingStatus.Queued],
            [ProcessingStatus.Queued] = [ProcessingStatus.Normalizing, ProcessingStatus.Failed],
            [ProcessingStatus.Normalizing] = [ProcessingStatus.OcrRunning, ProcessingStatus.Failed],
            [ProcessingStatus.OcrRunning] = [ProcessingStatus.Extracting, ProcessingStatus.Failed],
            [ProcessingStatus.Extracting] = [ProcessingStatus.Validating, ProcessingStatus.Failed],
            [ProcessingStatus.Validating] = [ProcessingStatus.Ready, ProcessingStatus.ReviewRequired, ProcessingStatus.Failed],
            [ProcessingStatus.ReviewRequired] = [ProcessingStatus.Validating, ProcessingStatus.Ready, ProcessingStatus.Failed],
            [ProcessingStatus.Ready] = [ProcessingStatus.Exporting, ProcessingStatus.Failed],
            [ProcessingStatus.Exporting] = [ProcessingStatus.Completed, ProcessingStatus.Failed],
            [ProcessingStatus.Completed] = [],
            [ProcessingStatus.Failed] = []
        };

    public InvoiceDocument(DocumentId id, SourceDocument source)
    {
        Id = id;
        Source = source;
        Status = ProcessingStatus.Uploaded;
        Currency = "PLN";
        Lines = [];
        VatSummaries = [];
        Issues = [];
    }

    public DocumentId Id { get; }
    public SourceDocument Source { get; }
    public DocumentType Type { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public InvoiceParty? Seller { get; private set; }
    public InvoiceParty? Buyer { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string Currency { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? BankAccount { get; private set; }
    public IReadOnlyList<InvoiceLine> Lines { get; private set; }
    public IReadOnlyList<VatSummary> VatSummaries { get; private set; }
    public InvoiceTotals? Totals { get; private set; }
    public IReadOnlyList<ValidationIssue> Issues { get; private set; }
    public int DataVersion { get; private set; }

    public void ApplyExtraction(DocumentType type, InvoiceParty? seller, InvoiceParty? buyer, string? invoiceNumber, DateOnly? issueDate, DateOnly? dueDate, string currency, string? paymentMethod, string? bankAccount, IReadOnlyList<InvoiceLine> lines, IReadOnlyList<VatSummary> vatSummaries, InvoiceTotals? totals)
    {
        Type = type;
        Seller = seller;
        Buyer = buyer;
        InvoiceNumber = invoiceNumber;
        IssueDate = issueDate;
        DueDate = dueDate;
        Currency = currency;
        PaymentMethod = paymentMethod;
        BankAccount = bankAccount;
        Lines = lines;
        VatSummaries = vatSummaries;
        Totals = totals;
        DataVersion++;
    }

    public void SetValidationIssues(IReadOnlyList<ValidationIssue> issues) => Issues = issues;

    public bool MoveTo(ProcessingStatus next)
    {
        if (!Transitions.TryGetValue(Status, out var allowed) || !allowed.Contains(next))
        {
            return false;
        }

        Status = next;
        return true;
    }
}
