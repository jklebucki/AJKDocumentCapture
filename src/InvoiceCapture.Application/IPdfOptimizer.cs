using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IPdfOptimizer
{
    Task<string> OptimizeAsync(InvoiceDocument document, CancellationToken cancellationToken);
}
