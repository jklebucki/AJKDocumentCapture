using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IInvoiceRepository
{
    Task AddAsync(InvoiceDocument document, CancellationToken cancellationToken);
    Task<InvoiceDocument?> GetAsync(DocumentId documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentSummary>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task UpdateAsync(InvoiceDocument document, CancellationToken cancellationToken);
}
