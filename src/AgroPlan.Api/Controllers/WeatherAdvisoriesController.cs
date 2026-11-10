using System.ComponentModel.DataAnnotations;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/weather-advisories")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class WeatherAdvisoriesController(IWeatherAdvisoryProvider provider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WeatherAdvisoryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WeatherAdvisoryResult>> Get(
        Guid farmId,
        [FromQuery, Required, StringLength(200, MinimumLength = 1)] string geographicArea,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        CancellationToken cancellationToken)
    {
        if (latitude.HasValue != longitude.HasValue
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
            return BadRequest(new { message = "Provide both valid coordinates or neither." });

        var now = DateTimeOffset.UtcNow;
        var request = new WeatherAdvisoryRequest(farmId, geographicArea.Trim(), latitude, longitude, now);
        var result = await provider.GetAsync(request, cancellationToken);
        return Ok(WeatherAdvisoryFreshness.Evaluate(result, request.GeographicArea, now));
    }
}
