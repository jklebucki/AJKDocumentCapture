using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IInvoiceValidator
{
    IReadOnlyList<ValidationIssue> Validate(InvoiceDocument document);
}
