using System.Text.Json;
using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using InRemedy.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/saved-views")]
public sealed class SavedViewsController(InRemedyDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedViewDto>>> GetSavedViews(CancellationToken cancellationToken)
    {
        var savedViews = await dbContext.SavedViews
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.PageType)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var response = savedViews.Select(ToDto).ToList();
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<SavedViewDto>> CreateSavedView([FromBody] CreateSavedViewRequest request, CancellationToken cancellationToken)
    {
        if (request.IsDefault)
        {
            var existingDefaults = await dbContext.SavedViews
                .Where(x => x.OwnerUserId == request.OwnerUserId && x.PageType == request.PageType && x.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existingDefault in existingDefaults)
            {
                existingDefault.IsDefault = false;
            }
        }

        var savedView = new SavedView
        {
            SavedViewId = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            PageType = request.PageType,
            Name = request.Name,
            IsDefault = request.IsDefault,
            IsSystemDefault = false,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
            ViewDefinitionJson = JsonSerializer.Serialize(request.ViewDefinition)
        };

        dbContext.SavedViews.Add(savedView);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSavedViews), ToDto(savedView));
    }

    [HttpPost("{savedViewId:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid savedViewId, CancellationToken cancellationToken)
    {
        var savedView = await dbContext.SavedViews.FirstOrDefaultAsync(x => x.SavedViewId == savedViewId, cancellationToken);
        if (savedView is null)
        {
            return NotFound();
        }

        var existingDefaults = await dbContext.SavedViews
            .Where(x => x.OwnerUserId == savedView.OwnerUserId && x.PageType == savedView.PageType && x.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var existingDefault in existingDefaults)
        {
            existingDefault.IsDefault = false;
        }

        savedView.IsDefault = true;
        savedView.ModifiedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{savedViewId:guid}")]
    public async Task<IActionResult> DeleteSavedView(Guid savedViewId, CancellationToken cancellationToken)
    {
        var savedView = await dbContext.SavedViews.FirstOrDefaultAsync(x => x.SavedViewId == savedViewId, cancellationToken);
        if (savedView is null)
        {
            return NotFound();
        }

        dbContext.SavedViews.Remove(savedView);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static SavedViewDto ToDto(SavedView savedView)
    {
        var viewDefinition = JsonSerializer.Deserialize<SavedViewDefinitionDto>(savedView.ViewDefinitionJson)
            ?? new SavedViewDefinitionDto(1, savedView.PageType, new { }, new { });

        return new SavedViewDto(
            savedView.SavedViewId,
            savedView.OwnerUserId,
            savedView.PageType,
            savedView.Name,
            savedView.IsDefault,
            savedView.IsSystemDefault,
            savedView.CreatedUtc,
            savedView.ModifiedUtc,
            viewDefinition);
    }
}
