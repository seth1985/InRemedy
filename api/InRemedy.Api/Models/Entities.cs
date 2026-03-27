namespace InRemedy.Api.Models;

public sealed class Remediation
{
    public Guid RemediationId { get; set; }
    public string RemediationName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Platform { get; set; } = "Windows";
    public bool ActiveFlag { get; set; }
    public string DetectionScriptVersion { get; set; } = string.Empty;
    public string RemediationScriptVersion { get; set; } = string.Empty;
    public ICollection<RemediationResult> Results { get; set; } = [];
}

public sealed class Device
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string PrimaryUser { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string UpdateRing { get; set; } = string.Empty;
    public DateTime LastSyncDateTimeUtc { get; set; }
    public ICollection<RemediationResult> Results { get; set; } = [];
}

public sealed class RemediationResult
{
    public Guid ResultId { get; set; }
    public Guid RemediationId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime RunTimestampUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RemediationAttemptedFlag { get; set; }
    public bool RemediationSucceededFlag { get; set; }
    public string DetectionOutputRaw { get; set; } = string.Empty;
    public string RemediationOutputRaw { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string OutputCategory { get; set; } = string.Empty;
    public string ScriptVersion { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Seed";
    public DateTime IngestionTimestampUtc { get; set; }
    public Remediation Remediation { get; set; } = null!;
    public Device Device { get; set; } = null!;
}

public sealed class SavedView
{
    public Guid SavedViewId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsSystemDefault { get; set; }
    public string ViewDefinitionJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
}

public sealed class ImportBatch
{
    public Guid ImportBatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileHashSha256 { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string ImportType { get; set; } = "RemediationResultsCsv";
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int ImportedRows { get; set; }
    public int ErrorRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? DuplicateOfImportBatchId { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public ICollection<ImportError> Errors { get; set; } = [];
}

public sealed class ImportError
{
    public Guid ImportErrorId { get; set; }
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RowSnapshotJson { get; set; } = string.Empty;
    public ImportBatch ImportBatch { get; set; } = null!;
}

public sealed class ImportStagingRow
{
    public Guid ImportStagingRowId { get; set; }
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string RemediationName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DetectionScriptVersion { get; set; } = string.Empty;
    public string RemediationScriptVersion { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string PrimaryUser { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string UpdateRing { get; set; } = string.Empty;
    public DateTime LastSyncDateTimeUtc { get; set; }
    public DateTime RunTimestampUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DetectionOutputRaw { get; set; } = string.Empty;
    public string RemediationOutputRaw { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string OutputCategory { get; set; } = string.Empty;
    public string ScriptVersion { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
}
