using System.Text;
using System.Text.Json;
using System.Net;
using InvoiceCapture.Application;
using InvoiceCapture.Domain;

namespace InvoiceCapture.Worker;

public sealed class DocumentProcessor(IInvoiceRepository invoices, IProcessingJobRepository jobs, IBlobStore blobStore, IOcrClient ocrClient, IInvoiceExtractionClient extractionClient, IInvoiceValidator validator, ILogger<DocumentProcessor> logger)
{
    public async Task ProcessAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var document = await invoices.GetAsync(job.DocumentId, cancellationToken) ?? throw new InvalidOperationException("The job document does not exist.");
        try
        {
            switch (job.Stage)
            {
                case nameof(ProcessingStatus.Queued):
                    Move(document, ProcessingStatus.Queued, ProcessingStatus.Normalizing);
                    await PersistAsync(job, document, ProcessingStatus.Normalizing, cancellationToken);
                    break;
                case nameof(ProcessingStatus.Normalizing):
                    Move(document, ProcessingStatus.OcrRunning);
                    await PersistAsync(job, document, ProcessingStatus.OcrRunning, cancellationToken);
                    break;
                case nameof(ProcessingStatus.OcrRunning):
                    await using (var source = await blobStore.OpenReadAsync(document.Source.OriginalPath, cancellationToken))
                    {
                        var ocr = await ocrClient.ExtractAsync(source, document.Source.MediaType, cancellationToken);
                        await SaveTextAsync(document.Id, "artifacts/ocr.json", ocr.RawJson, cancellationToken);
                    }
                    Move(document, ProcessingStatus.Extracting);
                    await PersistAsync(job, document, ProcessingStatus.Extracting, cancellationToken);
                    break;
                case nameof(ProcessingStatus.Extracting):
                    await using (var ocrStream = await blobStore.OpenReadAsync(Path.Combine(document.Id.ToString(), "artifacts", "ocr.json"), cancellationToken))
                    using (var reader = new StreamReader(ocrStream, Encoding.UTF8, leaveOpen: false))
                    {
                        var raw = await reader.ReadToEndAsync(cancellationToken);
                        var result = await extractionClient.ExtractAsync(new OcrResult(raw, FindMarkdown(raw), []), cancellationToken);
                        await SaveTextAsync(document.Id, "artifacts/extraction.json", result.CanonicalJson, cancellationToken);
                    }
                    Move(document, ProcessingStatus.Validating);
                    await PersistAsync(job, document, ProcessingStatus.Validating, cancellationToken);
                    break;
                case nameof(ProcessingStatus.Validating):
                    await ApplyAndValidateAsync(document, cancellationToken);
                    var target = document.Issues.Any(x => x.Severity == ValidationSeverity.Error) ? ProcessingStatus.ReviewRequired : ProcessingStatus.Ready;
                    Move(document, target);
                    await PersistAsync(job, document, target, cancellationToken);
                    break;
                default:
                    await jobs.FailAsync(job.Id, "invalid_stage", false, cancellationToken);
                    break;
            }
        }
        catch (HttpRequestException exception) when (IsRetryableProviderFailure(exception))
        {
            logger.LogWarning(exception, "Transient provider failure for job {JobId}.", job.Id);
            await jobs.FailAsync(job.Id, "provider_unavailable", true, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Non-retryable provider failure for job {JobId}.", job.Id);
            await jobs.FailAsync(job.Id, "provider_rejected_request", false, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Provider timeout for job {JobId}.", job.Id);
            await jobs.FailAsync(job.Id, "provider_timeout", true, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Processing failed for job {JobId}.", job.Id);
            await jobs.FailAsync(job.Id, "processing_failed", false, cancellationToken);
        }
    }

    private async Task ApplyAndValidateAsync(InvoiceDocument document, CancellationToken cancellationToken)
    {
        await using var extractionStream = await blobStore.OpenReadAsync(Path.Combine(document.Id.ToString(), "artifacts", "extraction.json"), cancellationToken);
        using var json = await JsonDocument.ParseAsync(extractionStream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var type = root.TryGetProperty("documentType", out var typeNode) && Enum.TryParse<DocumentType>(typeNode.GetString(), true, out var parsedType) ? parsedType : DocumentType.Unknown;
        var number = GetString(root, "invoiceNumber");
        var nip = GetString(root, "sellerNip");
        DateOnly? date = DateOnly.TryParse(GetString(root, "issueDate"), out var parsedDate) ? parsedDate : null;
        var currency = GetString(root, "currency") ?? "PLN";
        document.ApplyExtraction(type, new InvoiceParty(null, nip, null), null, number, date, null, currency, null, null, [], [], null);
        document.SetValidationIssues(validator.Validate(document));
    }

    private async Task PersistAsync(ProcessingJob job, InvoiceDocument document, ProcessingStatus stage, CancellationToken cancellationToken)
    {
        await invoices.UpdateAsync(document, cancellationToken);
        await jobs.CompleteStageAsync(job.Id, stage, cancellationToken);
    }

    private async Task SaveTextAsync(DocumentId id, string path, string value, CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(value));
        await blobStore.SaveArtifactAsync(id, path, content, cancellationToken);
    }

    private static void Move(InvoiceDocument document, params ProcessingStatus[] stages)
    {
        foreach (var stage in stages)
        {
            if (!document.MoveTo(stage)) { throw new InvalidOperationException("Invalid document status transition."); }
        }
    }

    private static string? GetString(JsonElement element, string propertyName) => element.TryGetProperty(propertyName, out var node) && node.ValueKind != JsonValueKind.Null ? node.GetString() : null;

    private static string FindMarkdown(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return FindMarkdown(document.RootElement);
    }

    private static string FindMarkdown(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("markdown") && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString() ?? string.Empty;
                }
                if (property.NameEquals("markdown") && property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("text", out var markdownText) && markdownText.ValueKind == JsonValueKind.String)
                {
                    return markdownText.GetString() ?? string.Empty;
                }

                var nested = FindMarkdown(property.Value);
                if (!string.IsNullOrEmpty(nested)) { return nested; }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindMarkdown(item);
                if (!string.IsNullOrEmpty(nested)) { return nested; }
            }
        }

        return string.Empty;
    }

    private static bool IsRetryableProviderFailure(HttpRequestException exception) =>
        !exception.StatusCode.HasValue ||
        exception.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)exception.StatusCode >= (int)HttpStatusCode.InternalServerError;
}
