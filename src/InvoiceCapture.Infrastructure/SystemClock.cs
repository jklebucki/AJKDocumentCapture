using InvoiceCapture.Application;

namespace InvoiceCapture.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
