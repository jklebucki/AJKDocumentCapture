using InvoiceCapture.Domain;

namespace InvoiceCapture.UnitTests;

public sealed class ProcessingJobTests
{
    [Fact]
    public void Rehydrate_preserves_the_persisted_stage_and_lease_state()
    {
        var documentId = new DocumentId(Guid.NewGuid());
        var leaseUntil = DateTimeOffset.UtcNow.AddMinutes(5);

        var job = ProcessingJob.Rehydrate(
            Guid.NewGuid(),
            documentId,
            "upload:example",
            ProcessingStatus.Queued,
            nameof(ProcessingStatus.Normalizing),
            2,
            "worker-1",
            leaseUntil,
            null);

        Assert.Equal(ProcessingStatus.Queued, job.Status);
        Assert.Equal(nameof(ProcessingStatus.Normalizing), job.Stage);
        Assert.Equal(2, job.Attempt);
        Assert.Equal("worker-1", job.LeaseOwner);
        Assert.Equal(leaseUntil, job.LeaseUntil);
    }
}
