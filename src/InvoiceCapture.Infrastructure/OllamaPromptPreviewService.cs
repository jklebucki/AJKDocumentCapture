using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public sealed class OllamaPromptPreviewService(IOptions<OllamaOptions> options)
{
    public OllamaPromptPreview GetPreview() => new(options.Value.Model, OllamaExtractionPrompt.SystemInstructions, OllamaExtractionPrompt.UserTemplate);
}
