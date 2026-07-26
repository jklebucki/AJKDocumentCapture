using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public interface IInvoiceXmlExporter
{
    string ProfileId { get; }
    Task<XmlExportResult> ExportAsync(InvoiceDocument document, CancellationToken cancellationToken);
}
