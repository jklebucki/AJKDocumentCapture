using System.Text.Json;
using InvoiceCapture.Application;

namespace InvoiceCapture.UnitTests;

public sealed class ComarchEcodKsefXmlPreviewRendererTests
{
    [Fact]
    public void Render_creates_the_base_ecod_ksef_xml_for_a_complete_extraction()
    {
        using var extraction = JsonDocument.Parse(CompleteExtraction);

        var result = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);

        Assert.Null(result.Message);
        Assert.NotNull(result.Xml);
        Assert.Contains("<Document-Invoice>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<Invoice-Header>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<TaxID>5260250274</TaxID>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<ItemDescription>Usługa</ItemDescription>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<Tax-Summary>", result.Xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_returns_a_preflight_message_instead_of_inventing_a_document_when_a_required_value_is_missing()
    {
        using var extraction = JsonDocument.Parse(CompleteExtraction.Replace("\"invoiceNumber\":\"FV/1\"", "\"invoiceNumber\":null", StringComparison.Ordinal));

        var result = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);

        Assert.Null(result.Xml);
        Assert.Contains("header.invoiceNumber", result.Message, StringComparison.Ordinal);
    }

    private const string CompleteExtraction = """
    {
      "documentType":"Invoice",
      "sourceBlockIds":[],
      "comarchEcodKsef":{
        "profile":"comarch-ecod-ksef-7.77",
        "header":{"invoiceNumber":"FV/1","invoiceDate":"2026-07-26","salesDate":"2026-07-26","invoiceCurrency":"PLN","documentFunctionCode":"O","messageType":"INV","invoicePaymentDueDate":null,"invoicePaymentMeans":null},
        "buyer":{"taxId":"5260250274","name":"Buyer","streetAndNumber":"Main 1","cityName":"Warsaw","postalCode":"00-001","country":"PL","vatPrefix":"PL","accountNumber":null},
        "seller":{"taxId":"5260250274","name":"Seller","streetAndNumber":"Main 2","cityName":"Warsaw","postalCode":"00-002","country":"PL","vatPrefix":"PL","accountNumber":null},
        "lines":[{"lineNumber":1,"itemDescription":"Usługa","invoiceQuantity":1,"unitOfMeasure":"C62","invoiceUnitNetPrice":100,"taxRate":23,"vatRate":23,"taxCategoryCode":"S","taxAmount":23,"netAmount":100,"grossAmount":123}],
        "summary":{"totalLines":1,"totalNetAmount":100,"totalTaxAmount":23,"totalGrossAmount":123,"taxSummary":[{"taxRate":23,"taxCategoryCode":"S","taxAmount":23,"taxableAmount":100,"grossAmount":123}]}
      }
    }
    """;
}
