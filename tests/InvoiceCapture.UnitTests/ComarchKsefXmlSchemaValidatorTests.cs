using System.Text.Json;
using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;

namespace InvoiceCapture.UnitTests;

public sealed class ComarchKsefXmlSchemaValidatorTests
{
    [Fact]
    public async Task ValidateAsync_accepts_xml_mapped_deterministically_from_normalized_facts()
    {
        using var extraction = JsonDocument.Parse(NormalizedExtraction);
        var preview = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);
        var validator = new ComarchKsefXmlSchemaValidator();

        var result = await validator.ValidateAsync(ComarchEcodKsefXmlPreviewRenderer.ProfileId, preview.Xml!, CancellationToken.None);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public async Task ValidateAsync_accepts_the_full_mapping_rendered_against_the_deployed_xsd()
    {
        using var extraction = JsonDocument.Parse(CompleteExtraction);
        var preview = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);
        var validator = new ComarchKsefXmlSchemaValidator();

        var result = await validator.ValidateAsync(ComarchEcodKsefXmlPreviewRenderer.ProfileId, preview.Xml!, CancellationToken.None);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public async Task ValidateAsync_rejects_an_element_that_is_not_defined_by_the_comarch_profile()
    {
        var validator = new ComarchKsefXmlSchemaValidator();

        var result = await validator.ValidateAsync(ComarchEcodKsefXmlPreviewRenderer.ProfileId, "<Document-Invoice><Unknown /></Document-Invoice>", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    private const string CompleteExtraction = """
    {"documentType":"Invoice","sourceBlockIds":[],"comarchEcodKsef":{"profile":"comarch-ecod-ksef-7.77","xml":{"name":"Document-Invoice","value":null,"children":[{"name":"Invoice-Header","value":null,"children":[{"name":"InvoiceNumber","value":"FV/1","children":[]},{"name":"InvoiceDate","value":"2026-07-26","children":[]},{"name":"InvoiceCurrency","value":"PLN","children":[]},{"name":"DocumentFunctionCode","value":"O","children":[]}]},{"name":"Invoice-Parties","value":null,"children":[{"name":"Buyer","value":null,"children":[{"name":"TaxID","value":"5260250274","children":[]},{"name":"Name","value":"Buyer","children":[]},{"name":"StreetAndNumber","value":"Main 1","children":[]},{"name":"Country","value":"PL","children":[]}]},{"name":"Seller","value":null,"children":[{"name":"TaxID","value":"5260250274","children":[]},{"name":"Name","value":"Seller","children":[]},{"name":"StreetAndNumber","value":"Main 2","children":[]},{"name":"Country","value":"PL","children":[]}]}]},{"name":"Invoice-Summary","value":null,"children":[{"name":"TotalLines","value":"0","children":[]},{"name":"TotalGrossAmount","value":"123.00","children":[]},{"name":"Tax-Summary","value":null,"children":[{"name":"Tax-Summary-Line","value":null,"children":[{"name":"TaxAmount","value":"23.00","children":[]},{"name":"TaxableAmount","value":"100.00","children":[]}]}]}]}]}}}
    """;

    private const string NormalizedExtraction = """
    {
      "status":"ready", "documentType":"invoice", "profile":"comarch-ecod-ksef-7.77", "issues":[], "evidence":[],
      "invoice":{
        "invoiceNumber":"FV/1", "invoiceDate":"2026-07-26", "salesDate":null, "invoicingPeriod":null, "currency":"PLN",
        "ksefDocumentNumber":"7822275815-20260701-617877C0001A-CB", "documentFunctionCode":"O",
        "buyer":{"taxId":"5260250274","vatPrefix":"PL","name":"Buyer","streetAndNumber":"Main 1","postalCode":null,"city":null,"country":"PL","email":null,"phone":null},
        "seller":{"taxId":"5260250274","vatPrefix":"PL","name":"Seller","streetAndNumber":"Main 2","postalCode":null,"city":null,"country":"PL","email":null,"phone":null},
        "lines":[],
        "summary":{"totalLines":0,"totalNetAmount":"100.00","totalTaxAmount":"23.00","totalGrossAmount":"123.00","taxLines":[{"taxRate":"23","taxCategoryCode":null,"taxableAmount":"100.00","taxAmount":"23.00","grossAmount":"123.00"}]}
      }
    }
    """;
}
