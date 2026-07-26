using InvoiceCapture.Infrastructure;

namespace InvoiceCapture.UnitTests;

public sealed class InvoiceValidatorTests
{
    [Theory]
    [InlineData("5260250274")]
    [InlineData("526-025-02-74")]
    public void IsValidPolishNip_AcceptsValidNip(string nip) => Assert.True(InvoiceValidator.IsValidPolishNip(nip));

    [Fact]
    public void IsValidPolishNip_RejectsInvalidChecksum() => Assert.False(InvoiceValidator.IsValidPolishNip("5260250275"));

    [Fact]
    public void IsValidIban_AcceptsPolishIban() => Assert.True(InvoiceValidator.IsValidIban("PL61109010140000071219812874"));
}
