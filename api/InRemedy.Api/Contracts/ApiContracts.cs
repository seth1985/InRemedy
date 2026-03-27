namespace InRemedy.Api.Contracts;

public sealed record DashboardSummaryDto(
    int TotalRemediations,
    int TotalDevices,
    int FailedResults,
    int RemediatedResults,
    int PassResults,
    int StaleResults,
    DateTime LastRefreshUtc);

public sealed record MetricBarDto(string Label, int Value, string Tone);

public sealed record ResultRowDto(
    Guid ResultId,
    Guid RemediationId,
    Guid DeviceId,
    string DeviceName,
    string PrimaryUser,
    string Manufacturer,
    string Model,
    string OsVersion,
    string OsBuild,
    string Region,
    string UpdateRing,
    DateTime LastSyncDateTimeUtc,
    string RemediationName,
    string RemediationCategory,
    string Platform,
    string DetectionScriptVersion,
    string RemediationScriptVersion,
    string Status,
    string OutputCategory,
    string DetectionOutputRaw,
    string RemediationOutputRaw,
    string? ErrorCode,
    string? ErrorSummary,
    string ScriptVersion,
    string DataSource,
    DateTime RunTimestampUtc);

public sealed record PagedResultsDto(
    IReadOnlyList<ResultRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record ResultsQueryRequestDto(
    string? Search,
    string? Status,
    Guid? RemediationId,
    Guid? DeviceId,
    string? Model,
    int Page = 1,
    int PageSize = 50,
    string? SortKey = null,
    string? SortDirection = null,
    Dictionary<string, string[]>? ColumnFilters = null,
    string[]? VisibleColumns = null);

public sealed record FilterOptionsRequestDto(
    string ColumnKey,
    string? Search,
    string? Status,
    Guid? RemediationId,
    Guid? DeviceId,
    string? Model,
    Dictionary<string, string[]>? ColumnFilters = null);

public sealed record RemediationDto(
    Guid RemediationId,
    string RemediationName,
    string Category,
    string Description,
    string Platform,
    bool ActiveFlag,
    string DetectionScriptVersion,
    string RemediationScriptVersion,
    int DevicesWithResults,
    int FailCount,
    int RemediatedCount,
    int PassCount);

public sealed record DeviceDto(
    Guid DeviceId,
    string DeviceName,
    string PrimaryUser,
    string Manufacturer,
    string Model,
    string OsVersion,
    string OsBuild,
    string Region,
    string UpdateRing,
    DateTime LastSyncDateTimeUtc,
    int FailCount);

public sealed record SavedViewDefinitionDto(
    int SchemaVersion,
    string PageType,
    object Filters,
    object GridState);

public sealed record SavedViewDto(
    Guid SavedViewId,
    string OwnerUserId,
    string PageType,
    string Name,
    bool IsDefault,
    bool IsSystemDefault,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    SavedViewDefinitionDto ViewDefinition);

public sealed record CreateSavedViewRequest(
    string OwnerUserId,
    string PageType,
    string Name,
    bool IsDefault,
    SavedViewDefinitionDto ViewDefinition);

public sealed record ImportErrorDto(
    Guid ImportErrorId,
    int RowNumber,
    string ColumnName,
    string ErrorMessage,
    string RowSnapshotJson);

public sealed record ImportBatchDto(
    Guid ImportBatchId,
    string FileName,
    string FileHashSha256,
    string ImportType,
    string Status,
    int TotalRows,
    int ProcessedRows,
    int ImportedRows,
    int ErrorRows,
    string Message,
    Guid? DuplicateOfImportBatchId,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    IReadOnlyList<ImportErrorDto> Errors);

public sealed record ImportColumnMappingDto(
    string CanonicalName,
    string? SourceHeader,
    bool Required,
    bool Mapped);

public sealed record ImportPreviewRowDto(
    int RowNumber,
    Dictionary<string, string> Values);

public sealed record ImportPreviewDto(
    string FileName,
    string FileHashSha256,
    bool CanImport,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    string[] MissingRequiredColumns,
    IReadOnlyList<ImportColumnMappingDto> ColumnMappings,
    IReadOnlyList<ImportPreviewRowDto> SampleRows,
    IReadOnlyList<ImportErrorDto> Errors);
