namespace InvoiceCapture.Application;

public static class ComarchInvoiceProfiles
{
    public const string Invoice = "comarch-ecod-invoice-7.77";
    public const string Correction = "comarch-ecod-correction-7.77";
    public const string Ksef = "comarch-ecod-ksef-7.77";
    public const string KsefCorrection = "comarch-ecod-ksef-correction-7.77";

    public static bool IsSupported(string? profileId) => profileId is Invoice or Correction or Ksef or KsefCorrection;
}
