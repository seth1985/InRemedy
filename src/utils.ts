import type { DerivedColumnDefinition, FiltersState, GridColumn, GridState, RemediationResult, Status } from "./types";

export type JoinedResult = RemediationResult;

export const derivedColumnKeyPrefix = "derived:";

export const statusOrder: Status[] = ["Fail", "Remediated", "Pass", "Stale", "Unknown", "No Data"];

const detectionStatusMap: Record<string, string> = {
  "0": "Unknown",
  "1": "Detection succeeded",
  "2": "Detection failed",
  "3": "Detection script error",
  "4": "Detection pending",
  "5": "Not applicable",
};

const remediationStatusMap: Record<string, string> = {
  "0": "Unknown",
  "1": "Remediation skipped",
  "2": "Remediated successfully",
  "3": "Remediation failed",
  "4": "Remediation script error",
  "5": "Reserved / future value",
};

export function applyFilters(rows: JoinedResult[], filters: FiltersState): JoinedResult[] {
  const query = filters.search.trim().toLowerCase();
  return rows.filter((row) => {
    const matchesSearch =
      !query ||
      row.deviceName.toLowerCase().includes(query) ||
      row.primaryUser.toLowerCase().includes(query) ||
      row.remediationName.toLowerCase().includes(query) ||
      row.outputCategory.toLowerCase().includes(query);
    const matchesStatuses = !filters.statuses.length || filters.statuses.includes(row.status);
    const matchesModels = !filters.models.length || filters.models.includes(row.model);
    const matchesRemediations = !filters.selectedRemediationIds.length || filters.selectedRemediationIds.includes(row.remediationId);
    const matchesDevices = !filters.selectedDeviceIds.length || filters.selectedDeviceIds.includes(row.deviceId);
    return matchesSearch && matchesStatuses && matchesModels && matchesRemediations && matchesDevices;
  });
}

export function applyColumnFilters(
  rows: JoinedResult[],
  columns: GridColumn[],
  columnFilters: Record<string, string[]>,
): JoinedResult[] {
  const activeFilters = Object.entries(columnFilters).filter(([, values]) => values.length > 0 && values.some((value) => value.trim() !== ""));
  if (!activeFilters.length) {
    return rows;
  }

  return rows.filter((row) =>
    activeFilters.every(([key, values]) => {
      const column = columns.find((entry) => entry.key === key);
      const cellValue = getCellDisplayValue(row, key);

      if (!column) {
        return true;
      }

      if (column.filter === "select") {
        return values.some((value) => cellValue.toLowerCase() === value.trim().toLowerCase());
      }

      return cellValue.toLowerCase().includes((values[0] ?? "").trim().toLowerCase());
    }),
  );
}

export function sortRows(rows: JoinedResult[], sortKey: string, direction: "asc" | "desc"): JoinedResult[] {
  const copy = [...rows];
  copy.sort((left, right) => {
    const leftValue = String(left[sortKey as keyof JoinedResult] ?? "");
    const rightValue = String(right[sortKey as keyof JoinedResult] ?? "");
    const comparison = leftValue.localeCompare(rightValue);
    return direction === "asc" ? comparison : -comparison;
  });
  return copy;
}

export function formatUtc(value: string): string {
  if (!value) {
    return "";
  }

  return new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
  }).format(new Date(value));
}

export function downloadCsv(rows: JoinedResult[]): void {
  const header = [
    "Device Name",
    "Primary User",
    "Manufacturer",
    "Model",
    "OS Version",
    "OS Build",
    "Region",
    "Update Ring",
    "Last Sync UTC",
    "Remediation",
    "Remediation Category",
    "Platform",
    "Detection Script Version",
    "Remediation Script Version",
    "Status",
    "Output Category",
    "Detection Output",
    "Remediation Output",
    "Error Code",
    "Error Summary",
    "Script Version",
    "Data Source",
    "Run Timestamp UTC",
  ];
  const lines = rows.map((row) =>
    [
      row.deviceName,
      row.primaryUser,
      row.manufacturer,
      row.model,
      row.osVersion,
      row.osBuild,
      row.region,
      row.updateRing,
      row.lastSyncDateTimeUtc,
      row.remediationName,
      row.remediationCategory,
      row.platform,
      getDisplayValueForColumnValue("detectionScriptVersion", row.detectionScriptVersion),
      getDisplayValueForColumnValue("remediationScriptVersion", row.remediationScriptVersion),
      row.status,
      row.outputCategory,
      row.detectionOutputRaw,
      row.remediationOutputRaw,
      row.errorCode ?? "",
      row.errorSummary ?? "",
      row.scriptVersion,
      row.dataSource,
      row.runTimestampUtc,
    ]
      .map((value) => `"${String(value).replaceAll('"', '""')}"`)
      .join(","),
  );
  const blob = new Blob([[header.join(","), ...lines].join("\n")], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "in-remedy-results.csv";
  anchor.click();
  URL.revokeObjectURL(url);
}

export function getDisplayValueForColumnValue(columnKey: string, value: string): string {
  if (columnKey === "detectionScriptVersion") {
    return detectionStatusMap[value] ?? value;
  }

  if (columnKey === "remediationScriptVersion") {
    return remediationStatusMap[value] ?? value;
  }

  return value;
}

export function isDerivedColumnKey(columnKey: string): boolean {
  return columnKey.startsWith(derivedColumnKeyPrefix);
}

export function getDerivedColumnValue(row: JoinedResult, columnKey: string, derivedColumns: DerivedColumnDefinition[]): string {
  const definition = derivedColumns.find((column) => column.key === columnKey);
  if (!definition) {
    return "";
  }

  const sourceValue = getCellDisplayValue(row, definition.sourceColumnKey, []);
  if (!sourceValue) {
    return "";
  }

  const parts = sourceValue.split(definition.delimiter).map((part) => part.trim());
  return parts[definition.partIndex] ?? "";
}

export function getCellDisplayValue(row: JoinedResult, key: string, derivedColumns: DerivedColumnDefinition[] = []): string {
  if (isDerivedColumnKey(key)) {
    return getDerivedColumnValue(row, key, derivedColumns);
  }

  const value = row[key as keyof JoinedResult];
  if (key === "runTimestampUtc" || key === "lastSyncDateTimeUtc") {
    return formatUtc(String(value ?? ""));
  }

  return getDisplayValueForColumnValue(key, String(value ?? ""));
}

export function buildAllGridColumns(baseColumns: GridColumn[], derivedColumns: DerivedColumnDefinition[]): GridColumn[] {
  return [
    ...baseColumns,
    ...derivedColumns.map((column) => ({
      key: column.key,
      label: column.label,
      sidebarLabel: column.label,
      filter: "text" as const,
      width: "180px",
      derived: true,
    })),
  ];
}

export function downloadGridCsv(rows: JoinedResult[], columns: GridColumn[], gridState: GridState): void {
  const visibleColumns = gridState.visibleColumns
    .map((columnKey) => columns.find((column) => column.key === columnKey))
    .filter((column): column is GridColumn => Boolean(column));

  const header = visibleColumns.map((column) => column.label);
  const lines = rows.map((row) =>
    visibleColumns
      .map((column) => getCellDisplayValue(row, column.key, gridState.derivedColumns))
      .map((value) => `"${String(value).replaceAll('"', '""')}"`)
      .join(","),
  );

  const blob = new Blob([[header.join(","), ...lines].join("\n")], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "in-remedy-results.csv";
  anchor.click();
  URL.revokeObjectURL(url);
}
