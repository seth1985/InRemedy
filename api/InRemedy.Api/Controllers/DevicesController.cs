using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetDevices([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Devices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.DeviceName.ToLower().Contains(term) || x.PrimaryUser.ToLower().Contains(term));
        }

        var response = await query
            .Select(x => new DeviceDto(
                x.DeviceId,
                x.DeviceName,
                x.PrimaryUser,
                x.Manufacturer,
                x.Model,
                x.OsVersion,
                x.OsBuild,
                x.Region,
                x.UpdateRing,
                x.LastSyncDateTimeUtc,
                x.Results.Count(r => r.Status == "Fail")))
            .OrderBy(x => x.DeviceName)
            .ToListAsync(cancellationToken);

        return Ok(response);
    }
}
