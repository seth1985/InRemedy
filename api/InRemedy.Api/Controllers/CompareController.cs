using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/compare")]
public sealed class CompareController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary(CancellationToken cancellationToken)
    {
        var failureComparison = await dbContext.RemediationResults
            .AsNoTracking()
            .Where(x => x.Status == "Fail")
            .GroupBy(x => x.Remediation.RemediationName)
            .Select(group => new MetricBarDto(group.Key, group.Count(), "status-fail"))
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);

        var overlapCount = await dbContext.Devices
            .AsNoTracking()
            .CountAsync(device => device.Results.Count(result => result.Status == "Fail") > 1, cancellationToken);

        return Ok(new
        {
            failureComparison,
            overlapCount
        });
    }
}
