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
}
