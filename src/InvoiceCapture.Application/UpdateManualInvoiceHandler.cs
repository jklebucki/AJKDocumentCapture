using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed class UpdateManualInvoiceHandler(IInvoiceRepository invoices, IInvoiceValidator validator)
{
    public async Task<InvoiceDocument?> HandleAsync(DocumentId documentId, ManualInvoiceUpdate update, CancellationToken cancellationToken)
    {
        var document = await invoices.GetAsync(documentId, cancellationToken);
        if (document is null) { return null; }

        var currency = string.IsNullOrWhiteSpace(update.Currency) ? "PLN" : update.Currency.Trim().ToUpperInvariant();
        var totals = update.NetAmount is not null && update.VatAmount is not null && update.GrossAmount is not null
            ? new InvoiceTotals(update.NetAmount.Value, update.VatAmount.Value, update.GrossAmount.Value)
            : null;
        document.ApplyExtraction(
            document.Type,
            new InvoiceParty(Trim(update.SellerName), Trim(update.SellerNip), document.Seller?.Address),
            new InvoiceParty(Trim(update.BuyerName), Trim(update.BuyerNip), document.Buyer?.Address),
            Trim(update.InvoiceNumber),
            update.IssueDate,
            update.DueDate,
            currency,
            Trim(update.PaymentMethod),
            Trim(update.BankAccount),
            document.Lines,
            document.VatSummaries,
            totals);
        document.SetValidationIssues(validator.Validate(document));
        if (document.Status == ProcessingStatus.ReviewRequired && !document.Issues.Any(x => x.Severity == ValidationSeverity.Error))
        {
            document.MoveTo(ProcessingStatus.Ready);
        }

        await invoices.UpdateAsync(document, cancellationToken);
        return document;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
