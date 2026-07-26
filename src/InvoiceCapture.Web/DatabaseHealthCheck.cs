using InvoiceCapture.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceCapture.Web;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceCaptureDbContext>();
        return await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database connection failed.");
    }
}
