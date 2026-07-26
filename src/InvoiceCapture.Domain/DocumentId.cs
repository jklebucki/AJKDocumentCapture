namespace InvoiceCapture.Domain;

public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
