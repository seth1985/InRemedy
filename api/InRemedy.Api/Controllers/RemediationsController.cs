using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/remediations")]
public sealed class RemediationsController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RemediationDto>>> GetRemediations(CancellationToken cancellationToken)
    {
        var response = await dbContext.Remediations
            .AsNoTracking()
            .OrderBy(x => x.RemediationName)
            .Select(x => new RemediationDto(
                x.RemediationId,
                x.RemediationName,
                x.Category,
                x.Description,
                x.Platform,
                x.ActiveFlag,
                x.DetectionScriptVersion,
                x.RemediationScriptVersion,
                x.Results.Select(r => r.DeviceId).Distinct().Count(),
                x.Results.Count(r => r.Status == "Fail"),
                x.Results.Count(r => r.Status == "Remediated"),
                x.Results.Count(r => r.Status == "Pass")))
            .ToListAsync(cancellationToken);

        return Ok(response);
    }
}
