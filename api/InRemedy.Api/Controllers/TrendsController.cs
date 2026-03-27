using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/trends")]
public sealed class TrendsController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<ActionResult<object>> GetRecent(CancellationToken cancellationToken)
    {
        var grouped = await dbContext.RemediationResults
            .AsNoTracking()
            .GroupBy(x => x.RunTimestampUtc.Date)
            .Select(group => new
            {
                Date = group.Key,
                Fail = group.Count(x => x.Status == "Fail"),
                Remediated = group.Count(x => x.Status == "Remediated")
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            failures = grouped.Select(x => new MetricBarDto(x.Date.ToString("dd MMM"), x.Fail, "status-fail")),
            remediated = grouped.Select(x => new MetricBarDto(x.Date.ToString("dd MMM"), x.Remediated, "status-remediated"))
        });
    }
}
