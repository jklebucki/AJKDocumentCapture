using InvoiceCapture.Domain;

namespace InvoiceCapture.Application;

public sealed record DocumentReviewResult(InvoiceDocument Document, string? ExtractionXml, string? ArtifactMessage);
