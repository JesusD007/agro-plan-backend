using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class TaskEvidenceTests
{
    [Fact]
    public void Task_evidence_submission_aggregate_is_available()
    {
        var aggregate = typeof(WorkerInvitation).Assembly.GetType("AgroPlan.Domain.Workforce.TaskEvidenceSubmission");
        Assert.NotNull(aggregate);
    }
}
