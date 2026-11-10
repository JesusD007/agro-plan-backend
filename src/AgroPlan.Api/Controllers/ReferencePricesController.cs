using System.ComponentModel.DataAnnotations;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/reference-prices")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class ReferencePricesController(IReferencePriceProvider provider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ReferencePriceResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReferencePriceResult>> Get(
        Guid farmId,
        [FromQuery, Required, StringLength(100, MinimumLength = 1)] string cropId,
        [FromQuery, StringLength(100)] string? variety,
        [FromQuery, Required, StringLength(100, MinimumLength = 1)] string marketChannel,
        [FromQuery, Required, StringLength(200, MinimumLength = 1)] string region,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new ReferencePriceRequest(farmId, cropId.Trim(), variety?.Trim(),
            marketChannel.Trim(), region.Trim(), now);
        var result = await provider.GetAsync(request, cancellationToken);
        return Ok(ReferencePriceFreshness.Evaluate(result, now));
    }
}
