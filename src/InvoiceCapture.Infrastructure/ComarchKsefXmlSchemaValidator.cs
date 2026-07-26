using System.Xml;
using System.Xml.Schema;
using InvoiceCapture.Application;

namespace InvoiceCapture.Infrastructure;

public sealed class ComarchKsefXmlSchemaValidator : IComarchInvoiceXmlValidator
{
    private static readonly IReadOnlyDictionary<string, string> SchemaFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ComarchInvoiceProfiles.Invoice] = "comarch-edi-invoice-7.77-invoice.xsd",
        [ComarchInvoiceProfiles.Correction] = "comarch-edi-invoice-7.77-correction.xsd",
        [ComarchInvoiceProfiles.Ksef] = "comarch-edi-invoice-7.77-ksef.xsd",
        [ComarchInvoiceProfiles.KsefCorrection] = "comarch-edi-invoice-7.77-ksef_correction.xsd"
    };
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly Dictionary<string, XmlSchemaSet> schemas = new(StringComparer.Ordinal);

    public async Task<ComarchXmlValidationResult> ValidateAsync(string profileId, string xml, CancellationToken cancellationToken)
    {
        if (!SchemaFiles.ContainsKey(profileId))
        {
            return new ComarchXmlValidationResult(false, ["Unsupported Comarch XML profile."]);
        }

        try
        {
            var schemaSet = await GetSchemasAsync(profileId, cancellationToken);
            var errors = new List<string>();
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints
            };
            settings.ValidationEventHandler += (_, eventArgs) => errors.Add(eventArgs.Message);
            using var source = new StringReader(xml);
            using var reader = XmlReader.Create(source, settings);
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new ComarchXmlValidationResult(errors.Count == 0, errors);
        }
        catch (FileNotFoundException)
        {
            return new ComarchXmlValidationResult(false, ["Comarch KSeF XSD is not deployed with the application."]);
        }
        catch (XmlException exception)
        {
            return new ComarchXmlValidationResult(false, [exception.Message]);
        }
        catch (XmlSchemaException exception)
        {
            return new ComarchXmlValidationResult(false, [exception.Message]);
        }
    }

    private async Task<XmlSchemaSet> GetSchemasAsync(string profileId, CancellationToken cancellationToken)
    {
        if (schemas.TryGetValue(profileId, out var cached)) { return cached; }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (schemas.TryGetValue(profileId, out cached)) { return cached; }
            var path = Path.Combine(AppContext.BaseDirectory, "specs", "ComarchInvoice", SchemaFiles[profileId]);
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            using var source = new StringReader(content);
            using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var loaded = new XmlSchemaSet();
            loaded.Add(null, reader);
            loaded.Compile();
            schemas[profileId] = loaded;
            return loaded;
        }
        finally
        {
            initializationLock.Release();
        }
    }
}
