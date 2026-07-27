using InvoiceCapture.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvoiceCapture.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInvoiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName).Validate(x => Path.IsPathRooted(x.Root), "Storage root must be absolute.").ValidateOnStart();
        services.AddOptions<OllamaOptions>().BindConfiguration(OllamaOptions.SectionName).Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Ollama BaseUrl must be absolute.").ValidateOnStart();
        services.AddOptions<PaddleOcrOptions>().BindConfiguration(PaddleOcrOptions.SectionName).Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Paddle OCR BaseUrl must be absolute.").ValidateOnStart();
        services.AddDbContext<InvoiceCaptureDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("Main")));
        services.AddScoped<IBlobStore, FileSystemBlobStore>();
        services.AddScoped<IInvoiceRepository, EfInvoiceRepository>();
        services.AddScoped<IProcessingJobRepository, EfProcessingJobRepository>();
        services.AddSingleton<IWorkerHeartbeat, FileSystemWorkerHeartbeat>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IInvoiceValidator, InvoiceValidator>();
        services.AddSingleton<IComarchInvoiceXmlValidator, ComarchKsefXmlSchemaValidator>();
        services.AddHttpClient<IOcrClient, PaddleOcrClient>((provider, client) => { var o = provider.GetRequiredService<IOptions<PaddleOcrOptions>>().Value; client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds); });
        services.AddHttpClient<IInvoiceExtractionClient, OllamaExtractionClient>((provider, client) => { var o = provider.GetRequiredService<IOptions<OllamaOptions>>().Value; client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds); });
        return services;
    }
}
