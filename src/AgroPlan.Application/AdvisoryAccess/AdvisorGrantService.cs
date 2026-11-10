using AgroPlan.Domain.AdvisoryAccess;

namespace AgroPlan.Application.AdvisoryAccess;

public sealed class AdvisorGrantService
{
    public AdvisorAccessGrant Create(Guid farmId, string advisorUserId,
        IReadOnlyCollection<AdvisorReportScope> scopes, string producerUserId, string purpose,
        DateTimeOffset startsAt, DateTimeOffset expiresAt, DateTimeOffset now) =>
        new(Guid.NewGuid(), farmId, advisorUserId, scopes, producerUserId, purpose, startsAt, expiresAt, now);
}
