using System.Globalization;
using InvoiceCapture.Application;
using InvoiceCapture.Domain;

namespace InvoiceCapture.Infrastructure;

public sealed class InvoiceValidator : IInvoiceValidator
{
    public IReadOnlyList<ValidationIssue> Validate(InvoiceDocument document)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(document.InvoiceNumber)) { issues.Add(new("required.invoiceNumber", ValidationSeverity.Error, "invoiceNumber", "Invoice number is required.")); }
        if (document.IssueDate is null) { issues.Add(new("required.issueDate", ValidationSeverity.Error, "issueDate", "Issue date is required.")); }
        if (string.IsNullOrWhiteSpace(document.Seller?.Nip)) { issues.Add(new("required.sellerNip", ValidationSeverity.Error, "seller.nip", "Seller NIP is required.")); }
        else if (!IsValidPolishNip(document.Seller.Nip)) { issues.Add(new("nip.checksum", ValidationSeverity.Error, "seller.nip", "Seller NIP checksum is invalid.")); }
        if (!string.IsNullOrWhiteSpace(document.BankAccount) && !IsValidIban(document.BankAccount)) { issues.Add(new("iban.checksum", ValidationSeverity.Warning, "bankAccount", "Bank account IBAN checksum is invalid.")); }
        if (document.DueDate is not null && document.IssueDate is not null && document.DueDate < document.IssueDate) { issues.Add(new("date.chronology", ValidationSeverity.Error, "dueDate", "Due date cannot precede issue date.")); }
        if (document.Totals is null) { issues.Add(new("required.totals", ValidationSeverity.Error, "totals", "Document totals are required.")); }
        else { ValidateAmounts(document, issues); }
        return issues;
    }

    public static bool IsValidPolishNip(string value)
    {
        var nip = new string(value.Where(char.IsDigit).ToArray());
        if (nip.Length != 10) { return false; }
        int[] weights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
        var sum = weights.Select((weight, index) => weight * (nip[index] - '0')).Sum();
        var checksum = sum % 11;
        return checksum != 10 && checksum == nip[9] - '0';
    }

    public static bool IsValidIban(string value)
    {
        var iban = new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (iban.Length < 15 || !iban[..2].All(char.IsLetter) || !iban[2..4].All(char.IsDigit)) { return false; }
        var reordered = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var character in reordered)
        {
            var token = char.IsLetter(character) ? (character - 'A' + 10).ToString(CultureInfo.InvariantCulture) : character.ToString();
            foreach (var digit in token) { remainder = (remainder * 10 + digit - '0') % 97; }
        }

        return remainder == 1;
    }

    private static void ValidateAmounts(InvoiceDocument document, List<ValidationIssue> issues)
    {
        var totals = document.Totals!;
        var lineNet = document.Lines.Sum(x => x.NetAmount);
        var lineVat = document.Lines.Sum(x => x.VatAmount);
        if (Math.Abs(lineNet - totals.NetAmount) > 0.02m) { issues.Add(new("total.linesNet", ValidationSeverity.Error, "totals.net", "Line net total does not match document total.")); }
        if (Math.Abs(totals.NetAmount + totals.VatAmount - totals.GrossAmount) > 0.02m) { issues.Add(new("total.gross", ValidationSeverity.Error, "totals.gross", "Net plus VAT does not equal gross total.")); }
        if (Math.Abs(lineVat - totals.VatAmount) > 0.02m) { issues.Add(new("total.linesVat", ValidationSeverity.Error, "totals.vat", "Line VAT total does not match document total.")); }
    }
}
