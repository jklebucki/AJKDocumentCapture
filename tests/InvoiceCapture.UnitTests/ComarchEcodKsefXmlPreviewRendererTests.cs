using System.Text.Json;
using InvoiceCapture.Application;

namespace InvoiceCapture.UnitTests;

public sealed class ComarchEcodKsefXmlPreviewRendererTests
{
    [Fact]
    public void Render_maps_normalized_facts_to_ordered_comarch_elements()
    {
        using var extraction = JsonDocument.Parse(NormalizedExtraction);

        var result = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);

        Assert.Null(result.Message);
        Assert.Contains("<InvoiceNumber>FV/1</InvoiceNumber>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<KSEFDocumentNumber>7822275815-20260701-617877C0001A-CB</KSEFDocumentNumber>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<Tax-Summary>", result.Xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_does_not_substitute_a_ksef_number_for_a_missing_invoice_number()
    {
        using var extraction = JsonDocument.Parse(NormalizedExtraction.Replace("\"invoiceNumber\":\"FV/1\"", "\"invoiceNumber\":null", StringComparison.Ordinal));

        var result = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);

        Assert.Null(result.Xml);
        Assert.Contains("invoiceNumber", result.Message, StringComparison.Ordinal);
    }

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
        Assert.Contains("<StreetAndNumber>Main 2</StreetAndNumber>", result.Xml, StringComparison.Ordinal);
        Assert.Contains("<Tax-Summary>", result.Xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_returns_a_message_when_the_xml_mapping_has_the_wrong_root()
    {
        using var extraction = JsonDocument.Parse(CompleteExtraction.Replace("\"name\":\"Document-Invoice\"", "\"name\":\"Unexpected\"", StringComparison.Ordinal));

        var result = ComarchEcodKsefXmlPreviewRenderer.Render(extraction.RootElement);

        Assert.Null(result.Xml);
        Assert.Contains("Document-Invoice", result.Message, StringComparison.Ordinal);
    }

    private const string CompleteExtraction = """
    {
      "documentType":"Invoice",
      "sourceBlockIds":[],
      "comarchEcodKsef":{
        "profile":"comarch-ecod-ksef-7.77",
        "xml":{"name":"Document-Invoice","value":null,"children":[
          {"name":"Invoice-Header","value":null,"children":[
            {"name":"InvoiceNumber","value":"FV/1","children":[]},
            {"name":"InvoiceDate","value":"2026-07-26","children":[]},
            {"name":"InvoiceCurrency","value":"PLN","children":[]},
            {"name":"DocumentFunctionCode","value":"O","children":[]}
          ]},
          {"name":"Invoice-Parties","value":null,"children":[
            {"name":"Buyer","value":null,"children":[
              {"name":"TaxID","value":"5260250274","children":[]},
              {"name":"Name","value":"Buyer","children":[]},
              {"name":"StreetAndNumber","value":"Main 1","children":[]},
              {"name":"Country","value":"PL","children":[]}
            ]},
            {"name":"Seller","value":null,"children":[
              {"name":"TaxID","value":"5260250274","children":[]},
              {"name":"Name","value":"Seller","children":[]},
              {"name":"StreetAndNumber","value":"Main 2","children":[]},
              {"name":"Country","value":"PL","children":[]}
            ]}
          ]},
          {"name":"Invoice-Summary","value":null,"children":[
            {"name":"TotalLines","value":"0","children":[]},
            {"name":"TotalGrossAmount","value":"123.00","children":[]},
            {"name":"Tax-Summary","value":null,"children":[
              {"name":"Tax-Summary-Line","value":null,"children":[
                {"name":"TaxAmount","value":"23.00","children":[]},
                {"name":"TaxableAmount","value":"100.00","children":[]}
              ]}
            ]}
          ]}
        ]}
      }
    }
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
