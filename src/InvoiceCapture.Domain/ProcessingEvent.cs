namespace InvoiceCapture.Domain;

public sealed record ProcessingEvent(DateTimeOffset OccurredAt, string Kind, string Stage, string Detail);
