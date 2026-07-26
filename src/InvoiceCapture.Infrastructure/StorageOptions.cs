namespace InvoiceCapture.Infrastructure;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string Root { get; init; } = "/data";
}
