using InvoiceCapture.Application;
using InvoiceCapture.Infrastructure;
using InvoiceCapture.Web;
using InvoiceCapture.Web.Components;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddInvoiceInfrastructure(builder.Configuration);
builder.Services.AddScoped<UploadDocumentHandler>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 25L * 1024 * 1024);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<InvoiceCaptureDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapPost("/api/documents", async (IFormFile file, HttpContext context, UploadDocumentHandler handler) =>
{
    if (file.Length == 0) { return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "An empty file cannot be uploaded."); }
    var key = context.Request.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(key)) { key = Guid.NewGuid().ToString("N"); }
    await using var content = file.OpenReadStream();
    try
    {
        var result = await handler.HandleAsync(new UploadRequest(file.FileName, file.ContentType, content, key), context.RequestAborted);
        return Results.Accepted($"/documents/{result.DocumentId}", result);
    }
    catch (ArgumentException exception) { return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: exception.Message); }
    catch (InvalidOperationException exception) { return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: exception.Message); }
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
