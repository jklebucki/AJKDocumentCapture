using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;

namespace InvoiceCapture.UnitTests;

public sealed class ComarchKsefSchemaGuideProviderTests
{
    [Fact]
    public async Task GetAsync_exposes_paths_for_each_deployed_comarch_profile()
    {
        var provider = new ComarchKsefSchemaGuideProvider();

        var guide = await provider.GetAsync(CancellationToken.None);

        Assert.Contains($"PROFILE {ComarchInvoiceProfiles.Invoice}", guide, StringComparison.Ordinal);
        Assert.Contains($"PROFILE {ComarchInvoiceProfiles.Correction}", guide, StringComparison.Ordinal);
        Assert.Contains($"PROFILE {ComarchInvoiceProfiles.Ksef}", guide, StringComparison.Ordinal);
        Assert.Contains($"PROFILE {ComarchInvoiceProfiles.KsefCorrection}", guide, StringComparison.Ordinal);
        Assert.Contains("/Document-Invoice/Invoice-Attachments", guide, StringComparison.Ordinal);
    }
}
