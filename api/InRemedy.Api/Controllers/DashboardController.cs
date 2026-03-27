using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var results = dbContext.RemediationResults.AsNoTracking();
        var lastRefreshUtc = await results.MaxAsync(x => (DateTime?)x.IngestionTimestampUtc, cancellationToken) ?? DateTime.UtcNow;

        return Ok(new DashboardSummaryDto(
            await dbContext.Remediations.CountAsync(cancellationToken),
            await dbContext.Devices.CountAsync(cancellationToken),
            await results.CountAsync(x => x.Status == "Fail", cancellationToken),
            await results.CountAsync(x => x.Status == "Remediated", cancellationToken),
            await results.CountAsync(x => x.Status == "Pass", cancellationToken),
            await results.CountAsync(x => x.Status == "Stale", cancellationToken),
            lastRefreshUtc));
    }

    [HttpGet("top-remediations")]
    public async Task<ActionResult<IReadOnlyList<MetricBarDto>>> GetTopRemediations(CancellationToken cancellationToken)
    {
        var response = await dbContext.RemediationResults
            .AsNoTracking()
            .Where(x => x.Status == "Fail")
            .GroupBy(x => x.Remediation.RemediationName)
            .Select(group => new MetricBarDto(group.Key, group.Count(), "status-fail"))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .ToListAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("top-models")]
    public async Task<ActionResult<IReadOnlyList<MetricBarDto>>> GetTopModels(CancellationToken cancellationToken)
    {
        var response = await dbContext.RemediationResults
            .AsNoTracking()
            .Where(x => x.Status == "Fail")
            .GroupBy(x => x.Device.Model)
            .Select(group => new MetricBarDto(group.Key, group.Count(), "status-remediated"))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .ToListAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("status-distribution")]
    public async Task<ActionResult<IReadOnlyList<MetricBarDto>>> GetStatusDistribution(CancellationToken cancellationToken)
    {
        var response = await dbContext.RemediationResults
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(group => new MetricBarDto(
                group.Key,
                group.Count(),
                group.Key == "Fail" ? "status-fail" : group.Key == "Remediated" ? "status-remediated" : group.Key == "Pass" ? "status-pass" : "status-muted"))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .ToListAsync(cancellationToken);

        return Ok(response);
    }
}
