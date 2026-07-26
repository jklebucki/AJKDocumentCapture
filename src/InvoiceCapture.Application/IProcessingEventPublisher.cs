using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IProcessingEventPublisher
{
    Task PublishAsync(DocumentId documentId, ProcessingStatus status, CancellationToken cancellationToken);
}
