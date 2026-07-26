using InvoiceCapture.Infrastructure;
using InvoiceCapture.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInvoiceInfrastructure(builder.Configuration);
builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddHostedService<DocumentWorker>();
await builder.Build().RunAsync();
