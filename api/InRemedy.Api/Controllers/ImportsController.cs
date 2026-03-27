using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using InRemedy.Api.Models;
using InRemedy.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace InRemedy.Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ImportsController(InRemedyDbContext dbContext, CsvImportService csvImportService, ImportQueue importQueue) : ControllerBase
{
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        const string csv = """
RemediationName,Category,Description,Platform,DetectionScriptVersion,RemediationScriptVersion,DeviceName,PrimaryUser,Manufacturer,Model,OsVersion,OsBuild,Region,UpdateRing,LastSyncDateTimeUtc,RunTimestampUtc,Status,DetectionOutputRaw,RemediationOutputRaw,ErrorCode,ErrorSummary,OutputCategory,ScriptVersion,DataSource
Fix Windows Update Service State,Windows Update,Ensures the Windows Update service is enabled and healthy.,Windows,1.8.0,1.4.0,LON-W11-2042,Adele Varga,Lenovo,ThinkPad T14 Gen 3,Windows 11 23H2,22631.3447,UK,Ring 0,2026-03-24T08:15:00Z,2026-03-24T10:00:00Z,Fail,ServiceStopped detected during validation on LON-W11-2042.,Corrective action attempted. Review service state and local policy.,0x87D1,Remediation script exited with a non-zero code.,ServiceStopped,1.4.0,CsvImport
""";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "in-remedy-template.csv");
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> GetImports(CancellationToken cancellationToken)
    {
        var batches = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(x => x.Errors)
            .OrderByDescending(x => x.StartedUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var response = batches.Select(ToDto).ToList();
        return Ok(response);
    }

    [HttpGet("column-labels")]
    public async Task<ActionResult<IReadOnlyDictionary<string, string>>> GetColumnLabels(CancellationToken cancellationToken)
    {
        var labels = await csvImportService.GetLatestColumnLabelsAsync(cancellationToken);
        return Ok(labels);
    }

    [HttpGet("{importBatchId:guid}")]
    public async Task<ActionResult<ImportBatchDto>> GetImport(Guid importBatchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(x => x.Errors)
            .FirstOrDefaultAsync(x => x.ImportBatchId == importBatchId, cancellationToken);

        return batch is null ? NotFound() : Ok(ToDto(batch));
    }

    [HttpPost("results-csv")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> ImportResultsCsv(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty CSV or ZIP file is required.");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV and ZIP files are supported.");
        }

        var batches = new List<ImportBatch>();
        if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await using var uploadStream = file.OpenReadStream();
            using var archive = new ZipArchive(uploadStream, ZipArchiveMode.Read, leaveOpen: false);
            var csvEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) && entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (csvEntries.Count == 0)
            {
                return BadRequest("The ZIP file does not contain any CSV files.");
            }

            foreach (var entry in csvEntries)
            {
                await using var entryStream = entry.Open();
                var batch = await csvImportService.QueueResultsCsvAsync($"{file.FileName}/{entry.Name}", entryStream, cancellationToken);
                await dbContext.Entry(batch).Collection(x => x.Errors).LoadAsync(cancellationToken);
                batches.Add(batch);

                if (batch.Status == "Queued")
                {
                    await importQueue.QueueAsync(batch.ImportBatchId, cancellationToken);
                }
            }
        }
        else
        {
            await using var stream = file.OpenReadStream();
            var batch = await csvImportService.QueueResultsCsvAsync(file.FileName, stream, cancellationToken);
            await dbContext.Entry(batch).Collection(x => x.Errors).LoadAsync(cancellationToken);
            batches.Add(batch);

            if (batch.Status == "Queued")
            {
                await importQueue.QueueAsync(batch.ImportBatchId, cancellationToken);
            }
        }

        return Ok(batches.Select(ToDto).ToList());
    }

    [HttpPost("preview-results-csv")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<ImportPreviewDto>> PreviewResultsCsv(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty CSV or ZIP file is required.");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV and ZIP files are supported.");
        }

        if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await using var uploadStream = file.OpenReadStream();
            using var archive = new ZipArchive(uploadStream, ZipArchiveMode.Read, leaveOpen: false);
            var firstCsvEntry = archive.Entries.FirstOrDefault(entry =>
                !string.IsNullOrWhiteSpace(entry.Name) && entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

            if (firstCsvEntry is null)
            {
                return BadRequest("The ZIP file does not contain any CSV files.");
            }

            await using var entryStream = firstCsvEntry.Open();
            var zipPreview = await csvImportService.PreviewResultsCsvAsync($"{file.FileName}/{firstCsvEntry.Name}", entryStream, cancellationToken);
            return Ok(zipPreview);
        }

        await using var stream = file.OpenReadStream();
        var preview = await csvImportService.PreviewResultsCsvAsync(file.FileName, stream, cancellationToken);
        return Ok(preview);
    }

    private static ImportBatchDto ToDto(ImportBatch batch) =>
        new(
            batch.ImportBatchId,
            batch.FileName,
            batch.FileHashSha256,
            batch.ImportType,
            batch.Status,
            batch.TotalRows,
            batch.ProcessedRows,
            batch.ImportedRows,
            batch.ErrorRows,
            batch.Message,
            batch.DuplicateOfImportBatchId,
            batch.StartedUtc,
            batch.CompletedUtc,
            batch.Errors
                .OrderBy(x => x.RowNumber)
                .ThenBy(x => x.ColumnName)
                .Select(x => new ImportErrorDto(x.ImportErrorId, x.RowNumber, x.ColumnName, x.ErrorMessage, x.RowSnapshotJson))
                .ToList());
}
