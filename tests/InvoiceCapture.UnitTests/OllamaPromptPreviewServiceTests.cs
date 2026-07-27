using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.UnitTests;

public sealed class OllamaPromptPreviewServiceTests
{
    [Fact]
    public void GetPreview_returns_a_compact_prompt_without_an_xsd_path_guide()
    {
        var service = new OllamaPromptPreviewService(Options.Create(new OllamaOptions()));

        var preview = service.GetPreview();

        Assert.Equal("gpt-oss:20b", preview.Model);
        Assert.DoesNotContain("XSD path guide", preview.SystemInstructions + preview.UserTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.True((preview.SystemInstructions.Length + preview.UserTemplate.Length) < 1_600);
    }
}
