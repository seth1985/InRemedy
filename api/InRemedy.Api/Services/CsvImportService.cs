using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using InRemedy.Api.Contracts;
using InRemedy.Api.Data;
using InRemedy.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace InRemedy.Api.Services;

public sealed class CsvImportService(InRemedyDbContext dbContext, IWebHostEnvironment environment)
{
    private const int MaxReportedErrors = 200;
    private const int PreviewSampleRowLimit = 5;
    private static readonly Dictionary<string, string> CanonicalToFrontendColumnKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RemediationName"] = "remediationName",
        ["Category"] = "remediationCategory",
        ["Description"] = "remediationDescription",
        ["Platform"] = "platform",
        ["DetectionScriptVersion"] = "detectionScriptVersion",
        ["RemediationScriptVersion"] = "remediationScriptVersion",
        ["DeviceName"] = "deviceName",
        ["PrimaryUser"] = "primaryUser",
        ["Manufacturer"] = "manufacturer",
        ["Model"] = "model",
        ["OsVersion"] = "osVersion",
        ["OsBuild"] = "osBuild",
        ["Region"] = "region",
        ["UpdateRing"] = "updateRing",
        ["LastSyncDateTimeUtc"] = "lastSyncDateTimeUtc",
        ["RunTimestampUtc"] = "runTimestampUtc",
        ["Status"] = "status",
        ["DetectionOutputRaw"] = "detectionOutputRaw",
        ["RemediationOutputRaw"] = "remediationOutputRaw",
        ["ErrorCode"] = "errorCode",
        ["ErrorSummary"] = "errorSummary",
        ["OutputCategory"] = "outputCategory",
        ["ScriptVersion"] = "scriptVersion",
        ["DataSource"] = "dataSource"
    };
    private static readonly Dictionary<string, string> DetectionScriptStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = "Unknown",
        ["1"] = "Detection succeeded",
        ["2"] = "Detection failed",
        ["3"] = "Detection script error",
        ["4"] = "Detection pending",
        ["5"] = "Not applicable"
    };

    private static readonly Dictionary<string, string> RemediationScriptStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = "Unknown",
        ["1"] = "Remediation skipped",
        ["2"] = "Remediated successfully",
        ["3"] = "Remediation failed",
        ["4"] = "Remediation script error",
        ["5"] = "Reserved / future value"
    };

    private static readonly string[] CanonicalHeaders =
    [
        "RemediationName", "Category", "Description", "Platform", "DetectionScriptVersion", "RemediationScriptVersion",
        "DeviceName", "PrimaryUser", "Manufacturer", "Model", "OsVersion", "OsBuild", "Region", "UpdateRing",
        "LastSyncDateTimeUtc", "RunTimestampUtc", "Status", "DetectionOutputRaw", "RemediationOutputRaw",
        "ErrorCode", "ErrorSummary", "OutputCategory", "ScriptVersion", "DataSource"
    ];

    private static readonly HashSet<string> OptionalHeaders =
    [
        "RemediationName", "Category", "Description", "Platform", "DetectionScriptVersion", "RemediationScriptVersion",
        "PrimaryUser", "LastSyncDateTimeUtc", "RunTimestampUtc", "Status",
        "Manufacturer", "OsBuild", "Region", "UpdateRing", "RemediationOutputRaw", "ErrorCode", "ErrorSummary",
        "OutputCategory", "ScriptVersion", "DataSource"
    ];

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RemediationName"] = ["RemediationName", "Remediation", "Remediation Name", "ScriptName", "DisplayName"],
        ["Category"] = ["Category", "RemediationCategory"],
        ["Description"] = ["Description", "RemediationDescription"],
        ["Platform"] = ["Platform", "OSPlatform", "OSDescription"],
        ["DetectionScriptVersion"] = ["DetectionScriptVersion", "DetectionVersion", "Detection Script Version", "InternalVersion"],
        ["RemediationScriptVersion"] = ["RemediationScriptVersion", "RemediationVersion", "Remediation Script Version", "InternalVersion"],
        ["DeviceName"] = ["DeviceName", "Device", "Device Name", "ManagedDeviceName"],
        ["PrimaryUser"] = ["UserName", "UserEmail", "UPN", "PrimaryUser", "User", "Primary User", "UserPrincipalName"],
        ["Manufacturer"] = ["Manufacturer", "Vendor"],
        ["Model"] = ["Model", "DeviceModel"],
        ["OsVersion"] = ["OsVersion", "OSVersion", "WindowsVersion"],
        ["OsBuild"] = ["OsBuild", "OSBuild", "Build", "BuildNumber"],
        ["Region"] = ["Region", "Country", "Geo"],
        ["UpdateRing"] = ["UpdateRing", "Ring", "DeploymentRing"],
        ["LastSyncDateTimeUtc"] = ["LastSyncDateTimeUtc", "LastSyncUtc", "LastSync", "Last Intune Sync", "LastAgentUpdateTime"],
        ["RunTimestampUtc"] = ["RunTimestampUtc", "RunTimeUtc", "RunTimestamp", "LastRunTimeUtc", "Run Time", "ModifiedTime"],
        ["Status"] = ["Status", "Result", "ResultStatus", "RemediationStatus", "DetectionStatus"],
        ["DetectionOutputRaw"] = ["DetectionOutputRaw", "DetectionOutput", "Detection Output", "PreRemediationDetectionScriptOutput", "PostRemediationDetectionScriptOutput"],
        ["RemediationOutputRaw"] = ["RemediationOutputRaw", "RemediationOutput", "Remediation Output", "RemediationScriptOutputDetails", "PostRemediationDetectionScriptOutput"],
        ["ErrorCode"] = ["ErrorCode", "Error Code"],
        ["ErrorSummary"] = ["ErrorSummary", "Error", "Error Summary", "RemediationScriptErrorDetails", "PostRemediationDetectionScriptError", "PreRemediationDetectionScriptError"],
        ["OutputCategory"] = ["OutputCategory", "CategoryNormalized", "NormalizedCategory", "DetectionScriptStatus", "RemediationScriptStatus"],
        ["ScriptVersion"] = ["ScriptVersion", "Version", "InternalVersion"],
        ["DataSource"] = ["DataSource", "Source"]
    };

    public async Task<ImportPreviewDto> PreviewResultsCsvAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var previewDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "preview");
        Directory.CreateDirectory(previewDirectory);
        var previewFilePath = Path.Combine(previewDirectory, $"{Guid.NewGuid():N}.csv");

        try
        {
            var fileHash = await CopyStreamToFileAndComputeSha256Async(stream, previewFilePath, cancellationToken);
            await using var previewStream = File.OpenRead(previewFilePath);
            var model = BuildImportModel(fileName, fileHash, previewStream, captureSampleRows: true);
            return BuildPreviewDto(model);
        }
        finally
        {
            if (File.Exists(previewFilePath))
            {
                File.Delete(previewFilePath);
            }
        }
    }

    public async Task<Dictionary<string, string>> GetLatestColumnLabelsAsync(CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.StoredFilePath) &&
                (x.Status == "Queued" || x.Status == "Running" || x.Status == "Completed" || x.Status == "CompletedWithErrors"))
            .OrderByDescending(x => x.StartedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (batch is null || string.IsNullOrWhiteSpace(batch.StoredFilePath) || !File.Exists(batch.StoredFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(batch.StoredFilePath);
        using var reader = new StreamReader(stream);
        using var rowEnumerator = ReadCsvRows(reader).GetEnumerator();
        if (!rowEnumerator.MoveNext())
        {
            return [];
        }

        var sourceHeaders = rowEnumerator.Current;
        var mapping = ResolveMapping(sourceHeaders);

        return mapping
            .Where(entry => CanonicalToFrontendColumnKey.ContainsKey(entry.Key))
            .ToDictionary(
                entry => CanonicalToFrontendColumnKey[entry.Key],
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ImportBatch> QueueResultsCsvAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "imports");
        Directory.CreateDirectory(storageDirectory);
        var storedFilePath = Path.Combine(storageDirectory, $"{Guid.NewGuid():N}.csv");
        var fileHash = await CopyStreamToFileAndComputeSha256Async(stream, storedFilePath, cancellationToken);

        await using var validationStream = File.OpenRead(storedFilePath);
        var model = BuildImportModel(fileName, fileHash, validationStream, captureSampleRows: false);

        var existingBatch = await dbContext.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.FileHashSha256 == model.FileHashSha256 &&
                     (x.Status == "Completed" || x.Status == "CompletedWithErrors" || x.Status == "Queued" || x.Status == "Running"),
                cancellationToken);

        var batch = new ImportBatch
        {
            ImportBatchId = Guid.NewGuid(),
            FileName = fileName,
            FileHashSha256 = model.FileHashSha256,
            StoredFilePath = storedFilePath,
            Status = "Queued",
            TotalRows = model.TotalRows,
            ErrorRows = model.CanImport ? 0 : model.ErrorRows,
            Message = model.CanImport ? "Import queued." : "Queued file contains validation errors and will not be processed.",
            StartedUtc = DateTime.UtcNow
        };

        if (existingBatch is not null)
        {
            batch.Status = "SkippedDuplicate";
            batch.Message = $"This file matches a previous import: {existingBatch.FileName}.";
            batch.DuplicateOfImportBatchId = existingBatch.ImportBatchId;
            batch.CompletedUtc = DateTime.UtcNow;
        }
        else if (!model.CanImport)
        {
            batch.Status = "Failed";
            batch.Message = model.MissingRequiredColumns.Count > 0
                ? $"Missing required columns: {string.Join(", ", model.MissingRequiredColumns)}"
                : "The CSV file contains validation errors.";
            batch.CompletedUtc = DateTime.UtcNow;
            foreach (var error in model.Errors.Take(MaxReportedErrors))
            {
                batch.Errors.Add(error);
            }
        }

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task ProcessQueuedImportAsync(Guid importBatchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.ImportBatchId == importBatchId, cancellationToken);

        if (batch is null || batch.Status != "Queued")
        {
            return;
        }

        batch.Status = "Running";
        batch.Message = "Import processing.";
        await dbContext.SaveChangesAsync(cancellationToken);

        await using var stream = File.OpenRead(batch.StoredFilePath);
        using var reader = new StreamReader(stream);
        using var rowEnumerator = ReadCsvRows(reader).GetEnumerator();
        if (!rowEnumerator.MoveNext())
        {
            batch.Status = "Failed";
            batch.Message = "Stored CSV file is empty.";
            batch.CompletedUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "ImportStagingRows" WHERE "ImportBatchId" = {importBatchId}""", cancellationToken);

        var sourceHeaders = rowEnumerator.Current;
        var mapping = ResolveMapping(sourceHeaders);
        var missingRequired = CanonicalHeaders.Where(x => !OptionalHeaders.Contains(x) && !mapping.ContainsKey(x)).ToList();
        if (missingRequired.Count > 0)
        {
            batch.Status = "Failed";
            batch.Message = $"Missing required columns: {string.Join(", ", missingRequired)}";
            batch.CompletedUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var copyConnection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        await copyConnection.OpenAsync(cancellationToken);

        await using var importer = await copyConnection.BeginBinaryImportAsync(
            """
COPY "ImportStagingRows" (
    "ImportStagingRowId","ImportBatchId","RowNumber","RemediationName","Category","Description","Platform",
    "DetectionScriptVersion","RemediationScriptVersion","DeviceName","PrimaryUser","Manufacturer","Model",
    "OsVersion","OsBuild","Region","UpdateRing","LastSyncDateTimeUtc","RunTimestampUtc","Status",
    "DetectionOutputRaw","RemediationOutputRaw","ErrorCode","ErrorSummary","OutputCategory","ScriptVersion","DataSource"
) FROM STDIN (FORMAT BINARY)
""",
            cancellationToken);

        var reportedErrors = new List<ImportError>();

        while (rowEnumerator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceRow = rowEnumerator.Current;
            if (sourceRow.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            
            batch.ProcessedRows++;
            var rowNumber = batch.ProcessedRows + 1;
            var normalizedRow = BuildNormalizedRow(sourceHeaders, sourceRow, mapping);

            var rowErrors = ValidateRow(normalizedRow, rowNumber);
            if (rowErrors.Count > 0)
            {
                batch.ErrorRows++;
                foreach (var error in rowErrors)
                {
                    if (reportedErrors.Count < MaxReportedErrors)
                    {
                        error.ImportBatchId = importBatchId;
                        error.ImportBatch = null!;
                        reportedErrors.Add(error);
                    }
                }
            }
            else
            {
                await importer.StartRowAsync(cancellationToken);
                importer.Write(Guid.NewGuid(), NpgsqlDbType.Uuid);
                importer.Write(importBatchId, NpgsqlDbType.Uuid);
                importer.Write(rowNumber, NpgsqlDbType.Integer);
                importer.Write(normalizedRow["RemediationName"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Category"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Description"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Platform"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["DetectionScriptVersion"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["RemediationScriptVersion"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["DeviceName"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["PrimaryUser"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Manufacturer"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Model"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["OsVersion"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["OsBuild"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["Region"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["UpdateRing"], NpgsqlDbType.Text);
                importer.Write(ParseUtc(normalizedRow["LastSyncDateTimeUtc"]), NpgsqlDbType.TimestampTz);
                importer.Write(ParseUtc(normalizedRow["RunTimestampUtc"]), NpgsqlDbType.TimestampTz);
                importer.Write(normalizedRow["Status"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["DetectionOutputRaw"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["RemediationOutputRaw"], NpgsqlDbType.Text);
                WriteNullable(importer, NullIfEmpty(normalizedRow["ErrorCode"]));
                WriteNullable(importer, NullIfEmpty(normalizedRow["ErrorSummary"]));
                importer.Write(normalizedRow["OutputCategory"], NpgsqlDbType.Text);
                importer.Write(normalizedRow["ScriptVersion"], NpgsqlDbType.Text);
                importer.Write(string.IsNullOrWhiteSpace(normalizedRow["DataSource"]) ? "CsvImport" : normalizedRow["DataSource"], NpgsqlDbType.Text);
            }

            if (batch.ProcessedRows % 1000 == 0)
            {
                batch.Message = $"Validating rows: {batch.ProcessedRows}/{batch.TotalRows}";
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await importer.CompleteAsync(cancellationToken);

        batch.Message = "Merging staged rows into operational tables.";
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO "Remediations" ("RemediationId","RemediationName","Category","Description","Platform","ActiveFlag","DetectionScriptVersion","RemediationScriptVersion")
SELECT gen_random_uuid(), s."RemediationName", MAX(s."Category"), MAX(s."Description"), MAX(s."Platform"), TRUE, MAX(s."DetectionScriptVersion"), MAX(s."RemediationScriptVersion")
FROM "ImportStagingRows" s
LEFT JOIN "Remediations" r ON r."RemediationName" = s."RemediationName"
WHERE s."ImportBatchId" = {importBatchId} AND r."RemediationId" IS NULL
GROUP BY s."RemediationName"
""", cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE "Remediations" r
SET "Category" = s."Category",
    "Description" = s."Description",
    "Platform" = s."Platform",
    "DetectionScriptVersion" = s."DetectionScriptVersion",
    "RemediationScriptVersion" = s."RemediationScriptVersion"
FROM (
    SELECT DISTINCT ON ("RemediationName")
        "RemediationName","Category","Description","Platform","DetectionScriptVersion","RemediationScriptVersion"
    FROM "ImportStagingRows"
    WHERE "ImportBatchId" = {importBatchId}
    ORDER BY "RemediationName","RowNumber" DESC
) s
WHERE r."RemediationName" = s."RemediationName"
""", cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO "Devices" ("DeviceId","DeviceName","PrimaryUser","Manufacturer","Model","OsVersion","OsBuild","Region","UpdateRing","LastSyncDateTimeUtc")
SELECT gen_random_uuid(), s."DeviceName", MAX(s."PrimaryUser"), MAX(s."Manufacturer"), MAX(s."Model"), MAX(s."OsVersion"), MAX(s."OsBuild"), MAX(s."Region"), MAX(s."UpdateRing"), MAX(s."LastSyncDateTimeUtc")
FROM "ImportStagingRows" s
LEFT JOIN "Devices" d ON d."DeviceName" = s."DeviceName"
WHERE s."ImportBatchId" = {importBatchId} AND d."DeviceId" IS NULL
GROUP BY s."DeviceName"
""", cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE "Devices" d
SET "PrimaryUser" = s."PrimaryUser",
    "Manufacturer" = s."Manufacturer",
    "Model" = s."Model",
    "OsVersion" = s."OsVersion",
    "OsBuild" = s."OsBuild",
    "Region" = s."Region",
    "UpdateRing" = s."UpdateRing",
    "LastSyncDateTimeUtc" = s."LastSyncDateTimeUtc"
FROM (
    SELECT DISTINCT ON ("DeviceName")
        "DeviceName","PrimaryUser","Manufacturer","Model","OsVersion","OsBuild","Region","UpdateRing","LastSyncDateTimeUtc"
    FROM "ImportStagingRows"
    WHERE "ImportBatchId" = {importBatchId}
    ORDER BY "DeviceName","RowNumber" DESC
) s
WHERE d."DeviceName" = s."DeviceName"
""", cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO "RemediationResults" (
    "ResultId","RemediationId","DeviceId","RunTimestampUtc","Status","RemediationAttemptedFlag","RemediationSucceededFlag",
    "DetectionOutputRaw","RemediationOutputRaw","ErrorCode","ErrorSummary","OutputCategory","ScriptVersion","DataSource","IngestionTimestampUtc"
)
SELECT
    gen_random_uuid(),
    r."RemediationId",
    d."DeviceId",
    s."RunTimestampUtc",
    s."Status",
    CASE WHEN s."Status" IN ('Fail','Remediated') THEN TRUE ELSE FALSE END,
    CASE WHEN s."Status" = 'Remediated' THEN TRUE ELSE FALSE END,
    s."DetectionOutputRaw",
    s."RemediationOutputRaw",
    s."ErrorCode",
    s."ErrorSummary",
    s."OutputCategory",
    s."ScriptVersion",
    s."DataSource",
    NOW()
FROM "ImportStagingRows" s
JOIN "Remediations" r ON r."RemediationName" = s."RemediationName"
JOIN "Devices" d ON d."DeviceName" = s."DeviceName"
WHERE s."ImportBatchId" = {importBatchId}
ON CONFLICT ("RemediationId","DeviceId","RunTimestampUtc") DO UPDATE
SET "Status" = EXCLUDED."Status",
    "RemediationAttemptedFlag" = EXCLUDED."RemediationAttemptedFlag",
    "RemediationSucceededFlag" = EXCLUDED."RemediationSucceededFlag",
    "DetectionOutputRaw" = EXCLUDED."DetectionOutputRaw",
    "RemediationOutputRaw" = EXCLUDED."RemediationOutputRaw",
    "ErrorCode" = EXCLUDED."ErrorCode",
    "ErrorSummary" = EXCLUDED."ErrorSummary",
    "OutputCategory" = EXCLUDED."OutputCategory",
    "ScriptVersion" = EXCLUDED."ScriptVersion",
    "DataSource" = EXCLUDED."DataSource",
    "IngestionTimestampUtc" = EXCLUDED."IngestionTimestampUtc"
""", cancellationToken);

        batch.ImportedRows = await dbContext.ImportStagingRows.CountAsync(x => x.ImportBatchId == importBatchId, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "ImportStagingRows" WHERE "ImportBatchId" = {importBatchId}""", cancellationToken);

        if (reportedErrors.Count > 0)
        {
            dbContext.ImportErrors.AddRange(reportedErrors);
        }

        batch.Status = batch.ErrorRows > 0 ? "CompletedWithErrors" : "Completed";
        batch.Message = batch.ErrorRows > 0
            ? $"Imported {batch.ImportedRows} rows with {batch.ErrorRows} error rows."
            : $"Imported {batch.ImportedRows} rows successfully.";
        batch.CompletedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ImportPreviewDto BuildPreviewDto(ImportModel model) =>
        new(
            model.FileName,
            model.FileHashSha256,
            model.CanImport,
            model.TotalRows,
            model.ValidRows,
            model.ErrorRows,
            model.MissingRequiredColumns.ToArray(),
            CanonicalHeaders.Select(header => new ImportColumnMappingDto(
                header,
                model.Mapping.TryGetValue(header, out var sourceHeader) ? sourceHeader : null,
                !OptionalHeaders.Contains(header),
                model.Mapping.ContainsKey(header))).ToList(),
            model.SampleValidRows.Select((row, index) => new ImportPreviewRowDto(index + 2, row)).ToList(),
            model.Errors.OrderBy(x => x.RowNumber).ThenBy(x => x.ColumnName)
                .Take(MaxReportedErrors)
                .Select(x => new ImportErrorDto(x.ImportErrorId, x.RowNumber, x.ColumnName, x.ErrorMessage, x.RowSnapshotJson))
                .ToList());

    private static ImportModel BuildImportModel(string fileName, string fileHash, Stream stream, bool captureSampleRows)
    {
        using var reader = new StreamReader(stream);
        using var rowEnumerator = ReadCsvRows(reader).GetEnumerator();
        if (!rowEnumerator.MoveNext())
        {
            return new ImportModel(fileName, fileHash)
            {
                CanImport = false,
                MissingRequiredColumns = CanonicalHeaders.Where(x => !OptionalHeaders.Contains(x)).ToList()
            };
        }

        var sourceHeaders = rowEnumerator.Current;
        var mapping = ResolveMapping(sourceHeaders);
        var missingRequired = CanonicalHeaders.Where(x => !OptionalHeaders.Contains(x) && !mapping.ContainsKey(x)).ToList();
        var sampleValidRows = new List<Dictionary<string, string>>();
        var errors = new List<ImportError>();
        var errorRowNumbers = new HashSet<int>();
        var totalRows = 0;
        var rowIndex = 1;

        while (rowEnumerator.MoveNext())
        {
            var sourceRow = rowEnumerator.Current;
            if (sourceRow.All(string.IsNullOrWhiteSpace))
            {
                rowIndex++;
                continue;
            }

            totalRows++;
            var rowNumber = rowIndex + 1;
            var normalizedRow = BuildNormalizedRow(sourceHeaders, sourceRow, mapping);

            if (missingRequired.Count > 0)
            {
                foreach (var missing in missingRequired)
                {
                    errorRowNumbers.Add(rowNumber);
                    if (errors.Count < MaxReportedErrors)
                    {
                        errors.Add(CreateError(rowNumber, missing, "No matching source column was found.", normalizedRow));
                    }
                }
                rowIndex++;
                continue;
            }

            var rowErrors = ValidateRow(normalizedRow, rowNumber);
            if (rowErrors.Count > 0)
            {
                errorRowNumbers.Add(rowNumber);
                foreach (var error in rowErrors)
                {
                    if (errors.Count < MaxReportedErrors)
                    {
                        errors.Add(error);
                    }
                }
                rowIndex++;
                continue;
            }

            if (captureSampleRows && sampleValidRows.Count < PreviewSampleRowLimit)
            {
                sampleValidRows.Add(normalizedRow);
            }

            rowIndex++;
        }

        return new ImportModel(fileName, fileHash)
        {
            CanImport = missingRequired.Count == 0,
            TotalRows = totalRows,
            ValidRows = totalRows - errorRowNumbers.Count,
            ErrorRows = errorRowNumbers.Count,
            MissingRequiredColumns = missingRequired,
            Mapping = mapping,
            SampleValidRows = sampleValidRows,
            Errors = errors
        };
    }

    private static Dictionary<string, string> ResolveMapping(IReadOnlyList<string> sourceHeaders)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonicalHeader in CanonicalHeaders)
        {
            var match = HeaderAliases[canonicalHeader].FirstOrDefault(alias =>
                sourceHeaders.Any(sourceHeader => string.Equals(NormalizeHeader(sourceHeader), NormalizeHeader(alias), StringComparison.OrdinalIgnoreCase)));
            if (match is null)
            {
                continue;
            }

            var sourceHeader = sourceHeaders.First(sourceHeader =>
                string.Equals(NormalizeHeader(sourceHeader), NormalizeHeader(match), StringComparison.OrdinalIgnoreCase));
            mapping[canonicalHeader] = sourceHeader;
        }

        return mapping;
    }

    private static string GetSourceValue(IReadOnlyList<string> sourceHeaders, IReadOnlyList<string> sourceRow, string sourceHeader)
    {
        var index = -1;
        for (var i = 0; i < sourceHeaders.Count; i++)
        {
            if (string.Equals(sourceHeaders[i], sourceHeader, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        return index >= 0 && index < sourceRow.Count ? sourceRow[index] : string.Empty;
    }

    private static Dictionary<string, string> BuildNormalizedRow(
        IReadOnlyList<string> sourceHeaders,
        IReadOnlyList<string> sourceRow,
        IReadOnlyDictionary<string, string> mapping)
    {
        var rowMap = CanonicalHeaders.ToDictionary(
            canonical => canonical,
            canonical => mapping.TryGetValue(canonical, out var sourceHeader)
                ? GetSourceValue(sourceHeaders, sourceRow, sourceHeader)
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);

        ApplyDerivedValues(sourceHeaders, sourceRow, rowMap);
        return rowMap;
    }

    private static void ApplyDerivedValues(
        IReadOnlyList<string> sourceHeaders,
        IReadOnlyList<string> sourceRow,
        Dictionary<string, string> rowMap)
    {
        var deviceName = FirstNonEmpty(rowMap["DeviceName"]);
        var model = FirstNonEmpty(rowMap["Model"]);
        var osVersion = FirstNonEmpty(rowMap["OsVersion"]);
        var osDescription = GetOptionalSourceValue(sourceHeaders, sourceRow, "OSDescription");
        var remediationStatus = GetOptionalSourceValue(sourceHeaders, sourceRow, "RemediationStatus");
        var detectionStatus = GetOptionalSourceValue(sourceHeaders, sourceRow, "DetectionStatus");
        var detectionScriptStatus = GetOptionalSourceValue(sourceHeaders, sourceRow, "DetectionScriptStatus");
        var remediationScriptStatus = GetOptionalSourceValue(sourceHeaders, sourceRow, "RemediationScriptStatus");

        rowMap["Category"] = FirstNonEmpty(rowMap["Category"], "Imported");
        rowMap["Description"] = FirstNonEmpty(rowMap["Description"], "Imported from Intune remediation CSV.");
        rowMap["RemediationName"] = FirstNonEmpty(
            rowMap["RemediationName"],
            GetOptionalSourceValue(sourceHeaders, sourceRow, "DisplayName"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PolicyId"),
            "Imported remediation");
        rowMap["Platform"] = NormalizePlatform(FirstNonEmpty(rowMap["Platform"], osDescription, "Windows"));
        rowMap["DetectionScriptVersion"] = NormalizeScriptExecutionStatus(
            FirstNonEmpty(GetOptionalSourceValue(sourceHeaders, sourceRow, "DetectionScriptStatus"), rowMap["DetectionScriptVersion"], rowMap["ScriptVersion"]),
            DetectionScriptStatusMap);
        rowMap["RemediationScriptVersion"] = NormalizeScriptExecutionStatus(
            FirstNonEmpty(GetOptionalSourceValue(sourceHeaders, sourceRow, "RemediationScriptStatus"), rowMap["RemediationScriptVersion"], rowMap["ScriptVersion"]),
            RemediationScriptStatusMap);
        rowMap["PrimaryUser"] = NormalizePrimaryUser(FirstNonEmpty(
            rowMap["PrimaryUser"],
            GetOptionalSourceValue(sourceHeaders, sourceRow, "UserName"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "UserEmail"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "UPN")));
        rowMap["Manufacturer"] = FirstNonEmpty(rowMap["Manufacturer"], InferManufacturer(model));
        rowMap["OsBuild"] = FirstNonEmpty(rowMap["OsBuild"], InferOsBuild(osVersion));
        rowMap["Region"] = FirstNonEmpty(rowMap["Region"], InferRegion(deviceName));
        rowMap["UpdateRing"] = FirstNonEmpty(rowMap["UpdateRing"], "Imported");
        rowMap["LastSyncDateTimeUtc"] = FirstNonEmpty(rowMap["LastSyncDateTimeUtc"], rowMap["RunTimestampUtc"]);
        rowMap["RunTimestampUtc"] = FirstNonEmpty(rowMap["RunTimestampUtc"], rowMap["LastSyncDateTimeUtc"]);
        rowMap["Status"] = NormalizeStatus(FirstNonEmpty(detectionScriptStatus, remediationScriptStatus, detectionStatus, rowMap["Status"], remediationStatus));
        rowMap["DetectionOutputRaw"] = FirstNonEmpty(
            rowMap["DetectionOutputRaw"],
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PreRemediationDetectionScriptOutput"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PostRemediationDetectionScriptOutput"));
        rowMap["RemediationOutputRaw"] = FirstNonEmpty(
            rowMap["RemediationOutputRaw"],
            GetOptionalSourceValue(sourceHeaders, sourceRow, "RemediationScriptOutputDetails"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PostRemediationDetectionScriptOutput"));
        rowMap["ErrorSummary"] = FirstNonEmpty(
            rowMap["ErrorSummary"],
            GetOptionalSourceValue(sourceHeaders, sourceRow, "RemediationScriptErrorDetails"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PostRemediationDetectionScriptError"),
            GetOptionalSourceValue(sourceHeaders, sourceRow, "PreRemediationDetectionScriptError"));
        rowMap["OutputCategory"] = FirstNonEmpty(rowMap["OutputCategory"], remediationScriptStatus, detectionScriptStatus, rowMap["Status"]);
        rowMap["ScriptVersion"] = FirstNonEmpty(rowMap["ScriptVersion"], rowMap["DetectionScriptVersion"], rowMap["RemediationScriptVersion"]);
        rowMap["DataSource"] = FirstNonEmpty(rowMap["DataSource"], "IntuneCsv");

        var fallbackTimestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        rowMap["LastSyncDateTimeUtc"] = FirstNonEmpty(rowMap["LastSyncDateTimeUtc"], fallbackTimestamp);
        rowMap["RunTimestampUtc"] = FirstNonEmpty(rowMap["RunTimestampUtc"], rowMap["LastSyncDateTimeUtc"], fallbackTimestamp);
    }

    private static string GetOptionalSourceValue(IReadOnlyList<string> sourceHeaders, IReadOnlyList<string> sourceRow, string sourceHeader)
        => sourceHeaders.Any(x => string.Equals(x, sourceHeader, StringComparison.Ordinal))
            ? GetSourceValue(sourceHeaders, sourceRow, sourceHeader)
            : string.Empty;

    private static string NormalizeHeader(string header)
        => new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static List<ImportError> ValidateRow(Dictionary<string, string> rowMap, int rowNumber)
    {
        var errors = new List<ImportError>();
        foreach (var header in CanonicalHeaders.Where(x => !OptionalHeaders.Contains(x)))
        {
            if (string.IsNullOrWhiteSpace(rowMap[header]))
            {
                errors.Add(CreateError(rowNumber, header, "Value is required.", rowMap));
            }
        }

        if (!TryParseUtc(rowMap["LastSyncDateTimeUtc"], out _))
        {
            errors.Add(CreateError(rowNumber, "LastSyncDateTimeUtc", "Invalid UTC datetime.", rowMap));
        }

        if (!TryParseUtc(rowMap["RunTimestampUtc"], out _))
        {
            errors.Add(CreateError(rowNumber, "RunTimestampUtc", "Invalid UTC datetime.", rowMap));
        }

        if (!new[] { "Pass", "Fail", "Remediated", "Unknown", "No Data", "Stale" }.Contains(rowMap["Status"]))
        {
            errors.Add(CreateError(rowNumber, "Status", "Status must be one of Pass, Fail, Remediated, Unknown, No Data, or Stale.", rowMap));
        }

        return errors;
    }

    private static ImportError CreateError(int rowNumber, string columnName, string message, Dictionary<string, string> rowMap) =>
        new()
        {
            ImportErrorId = Guid.NewGuid(),
            RowNumber = rowNumber,
            ColumnName = columnName,
            ErrorMessage = message,
            RowSnapshotJson = JsonSerializer.Serialize(rowMap)
        };

    private static DateTime ParseUtc(string value)
        => TryParseUtc(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid UTC datetime value '{value}'.");

    private static bool TryParseUtc(string value, out DateTime parsed)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return false;
        }

        if (parsed == DateTime.MinValue)
        {
            return false;
        }

        return true;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizePlatform(string value)
        => value.Contains("windows", StringComparison.OrdinalIgnoreCase) ? "Windows" : value;

    private static string NormalizePrimaryUser(string value)
    {
        if (Guid.TryParse(value, out _))
        {
            return "Unknown";
        }

        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static string InferManufacturer(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "Unknown";
        }

        var normalized = model.Trim();
        if (normalized.StartsWith("HP", StringComparison.OrdinalIgnoreCase))
        {
            return "HP";
        }

        if (normalized.StartsWith("Dell", StringComparison.OrdinalIgnoreCase))
        {
            return "Dell";
        }

        if (normalized.StartsWith("Lenovo", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("Think", StringComparison.OrdinalIgnoreCase))
        {
            return "Lenovo";
        }

        if (normalized.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("Surface", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft";
        }

        return "Unknown";
    }

    private static string InferOsBuild(string osVersion)
    {
        if (string.IsNullOrWhiteSpace(osVersion))
        {
            return "Unknown";
        }

        var segments = osVersion.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 3 ? segments[2] : osVersion;
    }

    private static string InferRegion(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return "Unknown";
        }

        var segments = deviceName.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 ? segments[0].ToUpperInvariant() : "Unknown";
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unknown";
        }

        if (new[] { "Pass", "Fail", "Remediated", "Unknown", "No Data", "Stale" }.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return new[] { "Pass", "Fail", "Remediated", "Unknown", "No Data", "Stale" }
                .First(x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var normalized = status.Trim();
        if (normalized.Contains("without issues", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("success", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("succeeded", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("no issue", StringComparison.OrdinalIgnoreCase))
        {
            return "Pass";
        }

        if (normalized.Contains("fixed", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("remediated", StringComparison.OrdinalIgnoreCase))
        {
            return "Remediated";
        }

        if (normalized.Contains("not run", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pending", StringComparison.OrdinalIgnoreCase))
        {
            return "Stale";
        }

        if (normalized is "4")
        {
            return "Pass";
        }

        if (normalized.Contains("issue", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return "Fail";
        }

        return "Unknown";
    }

    private static string NormalizeScriptExecutionStatus(string value, IReadOnlyDictionary<string, string> statusMap)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return statusMap.TryGetValue(value.Trim(), out var normalized) ? normalized : value.Trim();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static async Task<string> CopyStreamToFileAndComputeSha256Async(Stream source, string targetPath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var target = File.Create(targetPath);
        var buffer = new byte[1024 * 128];

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        sha256.TransformFinalBlock([], 0, 0);
        await target.FlushAsync(cancellationToken);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    private static IEnumerable<List<string>> ReadCsvRows(StreamReader reader)
    {
        var row = new List<string>();
        var field = "";
        var inQuotes = false;

        while (true)
        {
            var value = reader.Read();
            if (value == -1)
            {
                break;
            }

            var ch = (char)value;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        field += '"';
                        reader.Read();
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field += ch;
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    row.Add(field);
                    field = "";
                }
                else if (ch == '\n')
                {
                    row.Add(field.TrimEnd('\r'));
                    yield return row;
                    row = [];
                    field = "";
                }
                else
                {
                    field += ch;
                }
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.TrimEnd('\r'));
            yield return row;
        }
    }

    private static void WriteNullable(NpgsqlBinaryImporter importer, string? value)
    {
        if (value is null)
        {
            importer.WriteNull();
            return;
        }

        importer.Write(value, NpgsqlDbType.Text);
    }

    private sealed class ImportModel(string fileName, string fileHashSha256)
    {
        public string FileName { get; } = fileName;
        public string FileHashSha256 { get; } = fileHashSha256;
        public bool CanImport { get; init; }
        public int TotalRows { get; init; }
        public int ValidRows { get; init; }
        public int ErrorRows { get; init; }
        public List<string> MissingRequiredColumns { get; init; } = [];
        public Dictionary<string, string> Mapping { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Dictionary<string, string>> SampleValidRows { get; init; } = [];
        public List<ImportError> Errors { get; init; } = [];
    }

}
