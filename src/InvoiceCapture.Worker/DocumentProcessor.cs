using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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
            await jobs.RecordEventAsync(job.DocumentId, job.Id, "Started", job.Stage, DescribeStage(job.Stage), cancellationToken);
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
        if (!root.TryGetProperty("comarchEcodKsef", out var comarch) || comarch.ValueKind != JsonValueKind.Object)
        {
            ApplyLegacyExtraction(document, root, type);
            document.SetValidationIssues(validator.Validate(document));
            return;
        }

        if (comarch.TryGetProperty("xml", out _))
        {
            ApplyComarchXmlExtraction(document, root, type);
            document.SetValidationIssues(validator.Validate(document));
            return;
        }

        var header = GetObject(comarch, "header");
        var seller = GetObject(comarch, "seller");
        var buyer = GetObject(comarch, "buyer");
        var summary = GetObject(comarch, "summary");
        DateOnly? date = DateOnly.TryParse(GetString(header, "invoiceDate"), out var parsedDate) ? parsedDate : null;
        DateOnly? dueDate = DateOnly.TryParse(GetString(header, "invoicePaymentDueDate"), out var parsedDueDate) ? parsedDueDate : null;
        var lines = ParseLines(comarch);
        var vatSummaries = ParseVatSummaries(summary);
        var totals = ParseTotals(summary);
        document.ApplyExtraction(
            type,
            new InvoiceParty(GetString(seller, "name"), GetString(seller, "taxId"), GetString(seller, "streetAndNumber")),
            new InvoiceParty(GetString(buyer, "name"), GetString(buyer, "taxId"), GetString(buyer, "streetAndNumber")),
            GetString(header, "invoiceNumber"),
            date,
            dueDate,
            GetString(header, "invoiceCurrency") ?? "PLN",
            GetString(header, "invoicePaymentMeans"),
            GetString(seller, "accountNumber"),
            lines,
            vatSummaries,
            totals);
        document.SetValidationIssues(validator.Validate(document));
    }

    private static void ApplyComarchXmlExtraction(InvoiceDocument document, JsonElement root, DocumentType type)
    {
        var preview = ComarchEcodKsefXmlPreviewRenderer.Render(root);
        if (preview.Xml is null)
        {
            document.ApplyExtraction(type, null, null, null, null, null, "PLN", null, null, [], [], null);
            return;
        }

        var xml = XDocument.Parse(preview.Xml);
        var header = xml.Root?.Element("Invoice-Header");
        var seller = xml.Root?.Element("Invoice-Parties")?.Element("Seller");
        var buyer = xml.Root?.Element("Invoice-Parties")?.Element("Buyer");
        var summary = xml.Root?.Element("Invoice-Summary");
        DateOnly? issueDate = DateOnly.TryParse(Value(header, "InvoiceDate"), out var parsedIssueDate) ? parsedIssueDate : null;
        DateOnly? dueDate = DateOnly.TryParse(Value(header, "InvoicePaymentDueDate"), out var parsedDueDate) ? parsedDueDate : null;
        var vatSummaries = ParseComarchVatSummaries(summary);
        var grossAmount = GetDecimal(summary, "TotalGrossAmount");
        var totals = grossAmount is not null && vatSummaries.Count > 0
            ? new InvoiceTotals(vatSummaries.Sum(x => x.NetAmount), vatSummaries.Sum(x => x.VatAmount), grossAmount.Value)
            : null;
        document.ApplyExtraction(
            type,
            seller is null ? null : new InvoiceParty(Value(seller, "Name"), Value(seller, "TaxID"), Value(seller, "StreetAndNumber")),
            buyer is null ? null : new InvoiceParty(Value(buyer, "Name"), Value(buyer, "TaxID"), Value(buyer, "StreetAndNumber")),
            Value(header, "InvoiceNumber"),
            issueDate,
            dueDate,
            Value(header, "InvoiceCurrency") ?? "PLN",
            Value(header, "InvoicePaymentMeans"),
            Value(seller, "AccountNumber"),
            ParseComarchLines(xml.Root),
            vatSummaries,
            totals);
    }

    private static IReadOnlyList<InvoiceLine> ParseComarchLines(XElement? root)
    {
        if (root?.Element("Invoice-Lines") is not { } lines) { return []; }
        var parsed = new List<InvoiceLine>();
        foreach (var item in lines.Elements("Line").Select(line => line.Element("Line-Item")).Where(item => item is not null).Cast<XElement>())
        {
            var description = Value(item, "ItemDescription");
            var quantity = GetDecimal(item, "InvoiceQuantity");
            var netAmount = GetDecimal(item, "NetAmount");
            var vatRate = GetDecimal(item, "TaxRate");
            var vatAmount = GetDecimal(item, "TaxAmount");
            var grossAmount = GetDecimal(item, "GrossAmount");
            if (description is null || quantity is null || netAmount is null || vatRate is null || vatAmount is null || grossAmount is null) { continue; }
            parsed.Add(new InvoiceLine(description, Value(item, "UnitOfMeasure"), quantity.Value, netAmount.Value, vatRate.Value, vatAmount.Value, grossAmount.Value, []));
        }

        return parsed;
    }

    private static IReadOnlyList<VatSummary> ParseComarchVatSummaries(XElement? summary)
    {
        if (summary?.Element("Tax-Summary") is not { } taxSummary) { return []; }
        var parsed = new List<VatSummary>();
        foreach (var item in taxSummary.Elements("Tax-Summary-Line"))
        {
            var rate = GetDecimal(item, "TaxRate");
            var netAmount = GetDecimal(item, "TaxableAmount");
            var vatAmount = GetDecimal(item, "TaxAmount");
            if (rate is null || netAmount is null || vatAmount is null) { continue; }
            parsed.Add(new VatSummary(rate.Value, netAmount.Value, vatAmount.Value, netAmount.Value + vatAmount.Value));
        }

        return parsed;
    }

    private static void ApplyLegacyExtraction(InvoiceDocument document, JsonElement root, DocumentType type)
    {
        DateOnly? date = DateOnly.TryParse(GetString(root, "issueDate"), out var parsedDate) ? parsedDate : null;
        document.ApplyExtraction(
            type,
            new InvoiceParty(GetString(root, "sellerName"), GetString(root, "sellerNip"), null),
            new InvoiceParty(GetString(root, "buyerName"), GetString(root, "buyerNip"), null),
            GetString(root, "invoiceNumber"),
            date,
            null,
            GetString(root, "currency") ?? "PLN",
            null,
            null,
            [],
            [],
            null);
    }

    private static IReadOnlyList<InvoiceLine> ParseLines(JsonElement comarch)
    {
        if (!comarch.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) { return []; }
        var parsed = new List<InvoiceLine>();
        foreach (var line in lines.EnumerateArray())
        {
            var description = GetString(line, "itemDescription");
            var quantity = GetDecimal(line, "invoiceQuantity");
            var netAmount = GetDecimal(line, "netAmount");
            var vatRate = GetDecimal(line, "taxRate");
            var vatAmount = GetDecimal(line, "taxAmount");
            var grossAmount = GetDecimal(line, "grossAmount");
            if (description is null || quantity is null || netAmount is null || vatRate is null || vatAmount is null || grossAmount is null) { continue; }
            parsed.Add(new InvoiceLine(description, GetString(line, "unitOfMeasure"), quantity.Value, netAmount.Value, vatRate.Value, vatAmount.Value, grossAmount.Value, []));
        }

        return parsed;
    }

    private static IReadOnlyList<VatSummary> ParseVatSummaries(JsonElement summary)
    {
        if (!summary.TryGetProperty("taxSummary", out var taxSummary) || taxSummary.ValueKind != JsonValueKind.Array) { return []; }
        var parsed = new List<VatSummary>();
        foreach (var item in taxSummary.EnumerateArray())
        {
            var rate = GetDecimal(item, "taxRate");
            var netAmount = GetDecimal(item, "taxableAmount");
            var vatAmount = GetDecimal(item, "taxAmount");
            var grossAmount = GetDecimal(item, "grossAmount");
            if (rate is null || netAmount is null || vatAmount is null || grossAmount is null) { continue; }
            parsed.Add(new VatSummary(rate.Value, netAmount.Value, vatAmount.Value, grossAmount.Value));
        }

        return parsed;
    }

    private static InvoiceTotals? ParseTotals(JsonElement summary)
    {
        var netAmount = GetDecimal(summary, "totalNetAmount");
        var vatAmount = GetDecimal(summary, "totalTaxAmount");
        var grossAmount = GetDecimal(summary, "totalGrossAmount");
        return netAmount is not null && vatAmount is not null && grossAmount is not null
            ? new InvoiceTotals(netAmount.Value, vatAmount.Value, grossAmount.Value)
            : null;
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

    private static JsonElement GetObject(JsonElement element, string propertyName) => element.TryGetProperty(propertyName, out var node) && node.ValueKind == JsonValueKind.Object ? node : default;

    private static string? GetString(JsonElement element, string propertyName) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var node) && node.ValueKind != JsonValueKind.Null ? node.GetString() : null;

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var node) || node.ValueKind == JsonValueKind.Null) { return null; }
        return node.ValueKind == JsonValueKind.Number && node.TryGetDecimal(out var value)
            ? value
            : decimal.TryParse(node.GetString(), out value) ? value : null;
    }

    private static string? Value(XElement? element, string name) => element?.Element(name)?.Value;

    private static decimal? GetDecimal(XElement? element, string name) => decimal.TryParse(Value(element, name), out var value) ? value : null;

    private static string DescribeStage(string stage) => stage switch
    {
        nameof(ProcessingStatus.Queued) => "Invoice Capture Worker is preparing the job for processing.",
        nameof(ProcessingStatus.Normalizing) => "Invoice Capture Worker is normalizing the source document.",
        nameof(ProcessingStatus.OcrRunning) => "PaddleOCR-VL is performing OCR and layout analysis.",
        nameof(ProcessingStatus.Extracting) => "Ollama gpt-oss:20b is extracting structured invoice fields from OCR output.",
        nameof(ProcessingStatus.Validating) => "C# InvoiceValidator is applying deterministic validation rules.",
        _ => $"Invoice Capture Worker is processing {stage}."
    };

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
