namespace InvoiceCapture.Application;

public interface IComarchInvoiceXmlValidator
{
    Task<ComarchXmlValidationResult> ValidateAsync(string profileId, string xml, CancellationToken cancellationToken);
}
