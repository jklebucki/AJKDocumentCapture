namespace InvoiceCapture.Application;

public static class FileTypePolicy
{
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/tiff"] = ".tiff"
    };

    public static string Validate(string fileName, string contentType)
    {
        if (!Extensions.TryGetValue(contentType, out var extension))
        {
            throw new ArgumentException("Unsupported media type.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        return extension;
    }
}
