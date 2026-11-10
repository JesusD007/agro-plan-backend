using Microsoft.AspNetCore.Authorization;

namespace AgroPlan.Api.Authorization;

public sealed record AdvisorReportRequirement(AgroPlan.Domain.AdvisoryAccess.AdvisorReportScope Scope) : IAuthorizationRequirement;

public sealed record AdvisorReportResource(Guid FarmId, AgroPlan.Domain.AdvisoryAccess.AdvisorReportScope Scope);
