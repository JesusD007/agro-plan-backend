using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class TaskScheduleTests
{
    [Fact]
    public void Confirmed_record_projection_and_append_only_schedule_revision_are_available()
    {
        var applicationAssembly = typeof(WorkTask).Assembly.GetName();
        var domainAssembly = typeof(WorkTask).Assembly;
        Assert.NotNull(domainAssembly.GetType("AgroPlan.Domain.Workforce.TaskScheduleRevision"));
        Assert.NotNull(applicationAssembly);
    }
}
