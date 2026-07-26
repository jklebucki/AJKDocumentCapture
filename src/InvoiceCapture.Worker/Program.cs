using InvoiceCapture.Infrastructure;
using InvoiceCapture.Worker;

var builder = Host.CreateApplicationBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddInvoiceInfrastructure(builder.Configuration);
builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddHostedService<DocumentWorker>();
var host = builder.Build();

if (isDevelopment)
{
    await using var scope = host.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<InvoiceCaptureDbContext>();
    await DevelopmentDatabaseInitializer.EnsureCurrentSchemaAsync(database, CancellationToken.None);
}

await host.RunAsync();
