using InRemedy.Api.Data;
using InRemedy.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly InRemedyDbContext _dbContext;

    public AdminController(IConfiguration configuration, IHostApplicationLifetime lifetime, InRemedyDbContext dbContext)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        _dbContext = dbContext;
    }

    [HttpPost("reset-data")]
    public async Task<IActionResult> ResetData(CancellationToken cancellationToken)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("INREMEDY_CONNECTION_STRING") ??
            _configuration.GetConnectionString("InRemedy") ??
            "Host=localhost;Port=5432;Database=inremedy;Username=postgres;Password=postgres";

        await PostgresBootstrapper.ResetApplicationDataAsync(connectionString, cancellationToken);
        return NoContent();
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unavailable",
                message = "Database connection is not ready.",
            });
        }

        return Ok(new
        {
            status = "Healthy",
        });
    }

    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            _lifetime.StopApplication();
        });

        return Accepted();
    }
}
