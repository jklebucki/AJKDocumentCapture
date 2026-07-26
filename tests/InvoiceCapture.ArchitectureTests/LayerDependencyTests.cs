namespace InvoiceCapture.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotReferenceApplicationOrInfrastructure()
    {
        var references = typeof(InvoiceCapture.Domain.InvoiceDocument).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("InvoiceCapture.Application", references);
        Assert.DoesNotContain("InvoiceCapture.Infrastructure", references);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        var references = typeof(InvoiceCapture.Application.UploadDocumentHandler).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("InvoiceCapture.Infrastructure", references);
    }
}
