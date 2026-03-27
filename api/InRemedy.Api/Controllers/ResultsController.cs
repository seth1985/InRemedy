using System.Text;
using System.Linq.Expressions;
using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using InRemedy.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/results")]
public sealed class ResultsController(InRemedyDbContext dbContext) : ControllerBase
{
    private static readonly Dictionary<string, string> ExportHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deviceName"] = "Device Name",
        ["primaryUser"] = "Primary User",
        ["manufacturer"] = "Manufacturer",
        ["model"] = "Model",
        ["osVersion"] = "OS Version",
        ["osBuild"] = "OS Build",
        ["region"] = "Region",
        ["updateRing"] = "Update Ring",
        ["lastSyncDateTimeUtc"] = "Last Sync UTC",
        ["remediationName"] = "Remediation",
        ["remediationCategory"] = "Remediation Category",
        ["platform"] = "Platform",
        ["detectionScriptVersion"] = "Detection Script Version",
        ["remediationScriptVersion"] = "Remediation Script Version",
        ["status"] = "Status",
        ["outputCategory"] = "Detection Result",
        ["detectionOutputRaw"] = "Detection Output",
        ["remediationOutputRaw"] = "Remediation Output",
        ["errorCode"] = "Error Code",
        ["errorSummary"] = "Error Summary",
        ["scriptVersion"] = "Script Version",
        ["dataSource"] = "Data Source",
        ["runTimestampUtc"] = "Run Timestamp UTC"
    };

    [HttpGet]
    public async Task<ActionResult<PagedResultsDto>> GetResults(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? remediationId,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? model,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortKey = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 10, 250);

        var query = BuildFilteredQuery(search, status, remediationId, deviceId, model, Request.Query);
        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, sortKey, sortDirection);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(ProjectResultRow())
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultsDto(items, totalCount, normalizedPage, normalizedPageSize));
    }

    [HttpPost("query")]
    public async Task<ActionResult<PagedResultsDto>> QueryResults(
        [FromBody] ResultsQueryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, request.Page);
        var normalizedPageSize = Math.Clamp(request.PageSize, 10, 250);

        var query = BuildFilteredQuery(
            request.Search,
            request.Status,
            request.RemediationId,
            request.DeviceId,
            request.Model,
            request.ColumnFilters);
        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, request.SortKey, request.SortDirection);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(ProjectResultRow())
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultsDto(items, totalCount, normalizedPage, normalizedPageSize));
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetFilterOptions(
        [FromQuery] string columnKey,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? remediationId,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return BadRequest("A column key is required.");
        }

        var query = BuildFilteredQuery(search, status, remediationId, deviceId, model, Request.Query, columnKey);
        var normalizedColumnKey = columnKey.Trim();

        var options = normalizedColumnKey switch
        {
            "manufacturer" => await query.Select(x => x.Device.Manufacturer).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "model" => await query.Select(x => x.Device.Model).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "osVersion" => await query.Select(x => x.Device.OsVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "osBuild" => await query.Select(x => x.Device.OsBuild).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "region" => await query.Select(x => x.Device.Region).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "updateRing" => await query.Select(x => x.Device.UpdateRing).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationName" => await query.Select(x => x.Remediation.RemediationName).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationCategory" => await query.Select(x => x.Remediation.Category).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "platform" => await query.Select(x => x.Remediation.Platform).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "detectionScriptVersion" => await query.Select(x => x.Remediation.DetectionScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationScriptVersion" => await query.Select(x => x.Remediation.RemediationScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "status" => await query.Select(x => x.Status).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "outputCategory" => await query.Select(x => x.OutputCategory).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "detectionOutputRaw" => await query.Select(x => x.DetectionOutputRaw).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationOutputRaw" => await query.Select(x => x.RemediationOutputRaw).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "scriptVersion" => await query.Select(x => x.ScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "dataSource" => await query.Select(x => x.DataSource).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            _ => null
        };

        return options is null ? BadRequest("Unsupported column key.") : Ok(options);
    }

    [HttpPost("filter-options")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetFilterOptions(
        [FromBody] FilterOptionsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ColumnKey))
        {
            return BadRequest("A column key is required.");
        }

        var query = BuildFilteredQuery(
            request.Search,
            request.Status,
            request.RemediationId,
            request.DeviceId,
            request.Model,
            request.ColumnFilters,
            request.ColumnKey);
        var normalizedColumnKey = request.ColumnKey.Trim();

        var options = normalizedColumnKey switch
        {
            "manufacturer" => await query.Select(x => x.Device.Manufacturer).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "model" => await query.Select(x => x.Device.Model).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "osVersion" => await query.Select(x => x.Device.OsVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "osBuild" => await query.Select(x => x.Device.OsBuild).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "region" => await query.Select(x => x.Device.Region).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "updateRing" => await query.Select(x => x.Device.UpdateRing).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationName" => await query.Select(x => x.Remediation.RemediationName).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationCategory" => await query.Select(x => x.Remediation.Category).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "platform" => await query.Select(x => x.Remediation.Platform).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "detectionScriptVersion" => await query.Select(x => x.Remediation.DetectionScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationScriptVersion" => await query.Select(x => x.Remediation.RemediationScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "status" => await query.Select(x => x.Status).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "outputCategory" => await query.Select(x => x.OutputCategory).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "detectionOutputRaw" => await query.Select(x => x.DetectionOutputRaw).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "remediationOutputRaw" => await query.Select(x => x.RemediationOutputRaw).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "scriptVersion" => await query.Select(x => x.ScriptVersion).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            "dataSource" => await query.Select(x => x.DataSource).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            _ => null
        };

        return options is null ? BadRequest("Unsupported column key.") : Ok(options);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportResults(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? remediationId,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? model,
        [FromQuery] string? sortKey = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? visibleColumns = null,
        CancellationToken cancellationToken = default)
    {
        var requestedColumns = (visibleColumns ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(column => ExportHeaders.ContainsKey(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exportColumns = requestedColumns.Length > 0
            ? requestedColumns
            : ExportHeaders.Keys.ToArray();

        var rows = await ApplySorting(BuildFilteredQuery(search, status, remediationId, deviceId, model, Request.Query), sortKey, sortDirection)
            .Select(ProjectResultRow())
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", exportColumns.Select(column => EscapeCsv(ExportHeaders[column]))));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", exportColumns.Select(column => EscapeCsv(GetExportValue(row, column)))));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "in-remedy-results.csv");
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportResults(
        [FromBody] ResultsQueryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var requestedColumns = (request.VisibleColumns ?? Array.Empty<string>())
            .Where(column => ExportHeaders.ContainsKey(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exportColumns = requestedColumns.Length > 0
            ? requestedColumns
            : ExportHeaders.Keys.ToArray();

        var rows = await ApplySorting(
                BuildFilteredQuery(
                    request.Search,
                    request.Status,
                    request.RemediationId,
                    request.DeviceId,
                    request.Model,
                    request.ColumnFilters),
                request.SortKey,
                request.SortDirection)
            .Select(ProjectResultRow())
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", exportColumns.Select(column => EscapeCsv(ExportHeaders[column]))));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", exportColumns.Select(column => EscapeCsv(GetExportValue(row, column)))));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "in-remedy-results.csv");
    }

    private IQueryable<RemediationResult> BuildFilteredQuery(
        string? search,
        string? status,
        Guid? remediationId,
        Guid? deviceId,
        string? model,
        IQueryCollection queryString,
        string? excludeColumnFilterKey = null)
    {
        var query = dbContext.RemediationResults
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Device.DeviceName.ToLower().Contains(term) ||
                x.Device.PrimaryUser.ToLower().Contains(term) ||
                x.Remediation.RemediationName.ToLower().Contains(term) ||
                x.OutputCategory.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (remediationId.HasValue)
        {
            query = query.Where(x => x.RemediationId == remediationId.Value);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(x => x.DeviceId == deviceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(x => x.Device.Model == model);
        }

        foreach (var (columnKey, values) in ParseColumnFilters(queryString))
        {
            if (string.Equals(columnKey, excludeColumnFilterKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = ApplyColumnFilter(query, columnKey, values);
        }

        return query;
    }

    private IQueryable<RemediationResult> BuildFilteredQuery(
        string? search,
        string? status,
        Guid? remediationId,
        Guid? deviceId,
        string? model,
        Dictionary<string, string[]>? columnFilters,
        string? excludeColumnFilterKey = null)
    {
        var query = dbContext.RemediationResults
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Device.DeviceName.ToLower().Contains(term) ||
                x.Device.PrimaryUser.ToLower().Contains(term) ||
                x.Remediation.RemediationName.ToLower().Contains(term) ||
                x.OutputCategory.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (remediationId.HasValue)
        {
            query = query.Where(x => x.RemediationId == remediationId.Value);
        }

        if (deviceId.HasValue)
        {
            query = query.Where(x => x.DeviceId == deviceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(x => x.Device.Model == model);
        }

        foreach (var (columnKey, values) in ParseColumnFilters(columnFilters))
        {
            if (string.Equals(columnKey, excludeColumnFilterKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = ApplyColumnFilter(query, columnKey, values);
        }

        return query;
    }

    private static IEnumerable<KeyValuePair<string, List<string>>> ParseColumnFilters(IQueryCollection queryString)
    {
        foreach (var entry in queryString)
        {
            if (!entry.Key.StartsWith("cf_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = entry.Value
                .SelectMany(raw => (raw ?? string.Empty).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
            {
                continue;
            }

            yield return new KeyValuePair<string, List<string>>(entry.Key[3..], values);
        }
    }

    private static IEnumerable<KeyValuePair<string, List<string>>> ParseColumnFilters(Dictionary<string, string[]>? columnFilters)
    {
        if (columnFilters is null)
        {
            yield break;
        }

        foreach (var entry in columnFilters)
        {
            var values = entry.Value
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
            {
                continue;
            }

            yield return new KeyValuePair<string, List<string>>(entry.Key, values);
        }
    }

    private static IQueryable<RemediationResult> ApplyColumnFilter(IQueryable<RemediationResult> query, string columnKey, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return query;
        }

        return columnKey switch
        {
            "deviceName" => query.Where(x => x.Device.DeviceName.ToLower().Contains(values[0].ToLower())),
            "primaryUser" => query.Where(x => x.Device.PrimaryUser.ToLower().Contains(values[0].ToLower())),
            "manufacturer" => query.Where(x => values.Contains(x.Device.Manufacturer)),
            "model" => query.Where(x => values.Contains(x.Device.Model)),
            "osVersion" => query.Where(x => values.Contains(x.Device.OsVersion)),
            "osBuild" => query.Where(x => values.Contains(x.Device.OsBuild)),
            "region" => query.Where(x => values.Contains(x.Device.Region)),
            "updateRing" => query.Where(x => values.Contains(x.Device.UpdateRing)),
            "lastSyncDateTimeUtc" => query.Where(x => x.Device.LastSyncDateTimeUtc.ToString().ToLower().Contains(values[0].ToLower())),
            "remediationName" => query.Where(x => values.Contains(x.Remediation.RemediationName)),
            "remediationCategory" => query.Where(x => values.Contains(x.Remediation.Category)),
            "platform" => query.Where(x => values.Contains(x.Remediation.Platform)),
            "detectionScriptVersion" => query.Where(x => values.Contains(x.Remediation.DetectionScriptVersion)),
            "remediationScriptVersion" => query.Where(x => values.Contains(x.Remediation.RemediationScriptVersion)),
            "status" => query.Where(x => values.Contains(x.Status)),
            "outputCategory" => query.Where(x => values.Contains(x.OutputCategory)),
            "detectionOutputRaw" => query.Where(x => values.Contains(x.DetectionOutputRaw)),
            "remediationOutputRaw" => query.Where(x => values.Contains(x.RemediationOutputRaw)),
            "errorCode" => query.Where(x => (x.ErrorCode ?? string.Empty).ToLower().Contains(values[0].ToLower())),
            "errorSummary" => query.Where(x => (x.ErrorSummary ?? string.Empty).ToLower().Contains(values[0].ToLower())),
            "scriptVersion" => query.Where(x => values.Contains(x.ScriptVersion)),
            "dataSource" => query.Where(x => values.Contains(x.DataSource)),
            "runTimestampUtc" => query.Where(x => x.RunTimestampUtc.ToString().ToLower().Contains(values[0].ToLower())),
            _ => query
        };
    }

    private static IQueryable<RemediationResult> ApplySorting(IQueryable<RemediationResult> query, string? sortKey, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var normalizedSortKey = string.IsNullOrWhiteSpace(sortKey) ? "runTimestampUtc" : sortKey.Trim();

        return (normalizedSortKey, descending) switch
        {
            ("deviceName", false) => query.OrderBy(x => x.Device.DeviceName),
            ("deviceName", true) => query.OrderByDescending(x => x.Device.DeviceName),
            ("primaryUser", false) => query.OrderBy(x => x.Device.PrimaryUser),
            ("primaryUser", true) => query.OrderByDescending(x => x.Device.PrimaryUser),
            ("manufacturer", false) => query.OrderBy(x => x.Device.Manufacturer),
            ("manufacturer", true) => query.OrderByDescending(x => x.Device.Manufacturer),
            ("model", false) => query.OrderBy(x => x.Device.Model),
            ("model", true) => query.OrderByDescending(x => x.Device.Model),
            ("osVersion", false) => query.OrderBy(x => x.Device.OsVersion),
            ("osVersion", true) => query.OrderByDescending(x => x.Device.OsVersion),
            ("osBuild", false) => query.OrderBy(x => x.Device.OsBuild),
            ("osBuild", true) => query.OrderByDescending(x => x.Device.OsBuild),
            ("region", false) => query.OrderBy(x => x.Device.Region),
            ("region", true) => query.OrderByDescending(x => x.Device.Region),
            ("updateRing", false) => query.OrderBy(x => x.Device.UpdateRing),
            ("updateRing", true) => query.OrderByDescending(x => x.Device.UpdateRing),
            ("lastSyncDateTimeUtc", false) => query.OrderBy(x => x.Device.LastSyncDateTimeUtc),
            ("lastSyncDateTimeUtc", true) => query.OrderByDescending(x => x.Device.LastSyncDateTimeUtc),
            ("remediationName", false) => query.OrderBy(x => x.Remediation.RemediationName),
            ("remediationName", true) => query.OrderByDescending(x => x.Remediation.RemediationName),
            ("remediationCategory", false) => query.OrderBy(x => x.Remediation.Category),
            ("remediationCategory", true) => query.OrderByDescending(x => x.Remediation.Category),
            ("platform", false) => query.OrderBy(x => x.Remediation.Platform),
            ("platform", true) => query.OrderByDescending(x => x.Remediation.Platform),
            ("detectionScriptVersion", false) => query.OrderBy(x => x.Remediation.DetectionScriptVersion),
            ("detectionScriptVersion", true) => query.OrderByDescending(x => x.Remediation.DetectionScriptVersion),
            ("remediationScriptVersion", false) => query.OrderBy(x => x.Remediation.RemediationScriptVersion),
            ("remediationScriptVersion", true) => query.OrderByDescending(x => x.Remediation.RemediationScriptVersion),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("outputCategory", false) => query.OrderBy(x => x.OutputCategory),
            ("outputCategory", true) => query.OrderByDescending(x => x.OutputCategory),
            ("detectionOutputRaw", false) => query.OrderBy(x => x.DetectionOutputRaw),
            ("detectionOutputRaw", true) => query.OrderByDescending(x => x.DetectionOutputRaw),
            ("remediationOutputRaw", false) => query.OrderBy(x => x.RemediationOutputRaw),
            ("remediationOutputRaw", true) => query.OrderByDescending(x => x.RemediationOutputRaw),
            ("errorCode", false) => query.OrderBy(x => x.ErrorCode),
            ("errorCode", true) => query.OrderByDescending(x => x.ErrorCode),
            ("errorSummary", false) => query.OrderBy(x => x.ErrorSummary),
            ("errorSummary", true) => query.OrderByDescending(x => x.ErrorSummary),
            ("scriptVersion", false) => query.OrderBy(x => x.ScriptVersion),
            ("scriptVersion", true) => query.OrderByDescending(x => x.ScriptVersion),
            ("dataSource", false) => query.OrderBy(x => x.DataSource),
            ("dataSource", true) => query.OrderByDescending(x => x.DataSource),
            ("runTimestampUtc", false) => query.OrderBy(x => x.RunTimestampUtc),
            _ => query.OrderByDescending(x => x.RunTimestampUtc)
        };
    }

    private static Expression<Func<RemediationResult, ResultRowDto>> ProjectResultRow()
    {
        return x => new ResultRowDto(
            x.ResultId,
            x.RemediationId,
            x.DeviceId,
            x.Device.DeviceName,
            x.Device.PrimaryUser,
            x.Device.Manufacturer,
            x.Device.Model,
            x.Device.OsVersion,
            x.Device.OsBuild,
            x.Device.Region,
            x.Device.UpdateRing,
            x.Device.LastSyncDateTimeUtc,
            x.Remediation.RemediationName,
            x.Remediation.Category,
            x.Remediation.Platform,
            x.Remediation.DetectionScriptVersion,
            x.Remediation.RemediationScriptVersion,
            x.Status,
            x.OutputCategory,
            x.DetectionOutputRaw,
            x.RemediationOutputRaw,
            x.ErrorCode,
            x.ErrorSummary,
            x.ScriptVersion,
            x.DataSource,
            x.RunTimestampUtc);
    }

    private static string GetExportValue(ResultRowDto row, string columnKey)
    {
        return columnKey switch
        {
            "deviceName" => row.DeviceName,
            "primaryUser" => row.PrimaryUser,
            "manufacturer" => row.Manufacturer,
            "model" => row.Model,
            "osVersion" => row.OsVersion,
            "osBuild" => row.OsBuild,
            "region" => row.Region,
            "updateRing" => row.UpdateRing,
            "lastSyncDateTimeUtc" => row.LastSyncDateTimeUtc.ToString("O"),
            "remediationName" => row.RemediationName,
            "remediationCategory" => row.RemediationCategory,
            "platform" => row.Platform,
            "detectionScriptVersion" => row.DetectionScriptVersion,
            "remediationScriptVersion" => row.RemediationScriptVersion,
            "status" => row.Status,
            "outputCategory" => row.OutputCategory,
            "detectionOutputRaw" => row.DetectionOutputRaw,
            "remediationOutputRaw" => row.RemediationOutputRaw,
            "errorCode" => row.ErrorCode ?? string.Empty,
            "errorSummary" => row.ErrorSummary ?? string.Empty,
            "scriptVersion" => row.ScriptVersion,
            "dataSource" => row.DataSource,
            "runTimestampUtc" => row.RunTimestampUtc.ToString("O"),
            _ => string.Empty
        };
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
