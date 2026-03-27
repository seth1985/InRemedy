export type Status = "Pass" | "Fail" | "Remediated" | "Unknown" | "No Data" | "Stale";

export type PageId = "remediations";

export interface Remediation {
  remediationId: string;
  remediationName: string;
  category: string;
  description: string;
  platform: string;
  activeFlag: boolean;
  detectionScriptVersion: string;
  remediationScriptVersion: string;
  devicesWithResults: number;
  failCount: number;
  remediatedCount: number;
  passCount: number;
}

export interface RemediationResult {
  resultId: string;
  remediationId: string;
  deviceId: string;
  deviceName: string;
  primaryUser: string;
  manufacturer: string;
  model: string;
  osVersion: string;
  osBuild: string;
  region: string;
  updateRing: string;
  lastSyncDateTimeUtc: string;
  remediationName: string;
  remediationCategory: string;
  platform: string;
  detectionScriptVersion: string;
  remediationScriptVersion: string;
  runTimestampUtc: string;
  status: Status;
  detectionOutputRaw: string;
  remediationOutputRaw: string;
  errorCode?: string;
  errorSummary?: string;
  outputCategory: string;
  scriptVersion: string;
  dataSource: string;
}

export interface PagedResults {
  items: RemediationResult[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GridColumn {
  key: string;
  label: string;
  sidebarLabel?: string;
  filter: "text" | "select";
  width: string;
  derived?: boolean;
}

export interface DerivedColumnDefinition {
  key: string;
  label: string;
  sourceColumnKey: string;
  delimiter: string;
  delimiterLabel: string;
  partIndex: number;
}

export interface GridState {
  visibleColumns: string[];
  sortKey: string;
  sortDirection: "asc" | "desc";
  columnFilters: Record<string, string[]>;
  columnWidths: Record<string, number>;
  derivedColumns: DerivedColumnDefinition[];
}

export interface FiltersState {
  search: string;
  statuses: Status[];
  models: string[];
  selectedRemediationIds: string[];
  selectedDeviceIds: string[];
}

export interface SavedViewDefinition {
  schemaVersion: number;
  pageType: PageId;
  filters: FiltersState;
  gridState: GridState;
}

export interface SavedView {
  savedViewId: string;
  ownerUserId: string;
  pageType: PageId;
  name: string;
  isDefault: boolean;
  isSystemDefault: boolean;
  createdUtc: string;
  modifiedUtc: string;
  viewDefinition: SavedViewDefinition;
}

export interface ImportError {
  importErrorId: string;
  rowNumber: number;
  columnName: string;
  errorMessage: string;
  rowSnapshotJson: string;
}

export interface ImportBatch {
  importBatchId: string;
  fileName: string;
  fileHashSha256: string;
  importType: string;
  status: string;
  totalRows: number;
  processedRows: number;
  importedRows: number;
  errorRows: number;
  message: string;
  duplicateOfImportBatchId?: string | null;
  startedUtc: string;
  completedUtc?: string | null;
  errors: ImportError[];
}

export interface ImportColumnMapping {
  canonicalName: string;
  sourceHeader?: string | null;
  required: boolean;
  mapped: boolean;
}

export interface ImportPreviewRow {
  rowNumber: number;
  values: Record<string, string>;
}

export interface ImportPreview {
  fileName: string;
  fileHashSha256: string;
  canImport: boolean;
  totalRows: number;
  validRows: number;
  errorRows: number;
  missingRequiredColumns: string[];
  columnMappings: ImportColumnMapping[];
  sampleRows: ImportPreviewRow[];
  errors: ImportError[];
}

export interface WorkspaceState {
  activePage: PageId;
  filters: FiltersState;
  gridState: GridState;
  pageSize: number;
}
