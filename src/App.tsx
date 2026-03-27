import { useEffect, useMemo, useRef, useState, type ChangeEvent, type Dispatch, type ReactNode, type SetStateAction } from "react";
import { api, type ResultsQueryPayload } from "./api";
import { defaultFilters, defaultGridState, gridColumns } from "./data";
import logoImage from "../image/logo.png";
import type {
  DerivedColumnDefinition,
  FiltersState,
  GridColumn,
  GridState,
  ImportBatch,
  PagedResults,
  PageId,
  Remediation,
  Status,
  WorkspaceState,
} from "./types";
import {
  buildAllGridColumns,
  derivedColumnKeyPrefix,
  downloadGridCsv,
  formatUtc,
  getCellDisplayValue,
  getDisplayValueForColumnValue,
  isDerivedColumnKey,
  statusOrder,
  type JoinedResult,
} from "./utils";

const statusColors: Record<Status, string> = {
  Pass: "status-pass",
  Fail: "status-fail",
  Remediated: "status-remediated",
  Unknown: "status-muted",
  "No Data": "status-muted",
  Stale: "status-muted",
};

const workspaceStateStorageKey = "in-remedy.workspace-state";

type ImportQueueItem = {
  key: string;
  name: string;
  status: "selected" | "uploading" | "accepted" | "failed";
  message?: string;
};

type DelimiterPreset = "comma" | "semicolon" | "pipe" | "colon" | "tab" | "custom";

type DelimiterModalState = {
  sourceColumnKey: string;
  preset: DelimiterPreset;
  customDelimiter: string;
};

function RemediationStatusCard(props: { remediation: Remediation }) {
  const total = Math.max(props.remediation.devicesWithResults, 1);
  const detectionPassWidth = `${(props.remediation.passCount / total) * 100}%`;
  const detectionFailWidth = `${(props.remediation.failCount / total) * 100}%`;
  const remediationFixedWidth = `${(props.remediation.remediatedCount / total) * 100}%`;
  const remediationRemainingWidth = `${(props.remediation.failCount / total) * 100}%`;

  return (
    <section className="card intune-summary-card">
      <div className="eyebrow">{props.remediation.category}</div>
      <h3>{props.remediation.remediationName}</h3>
      <p>{props.remediation.description}</p>

      <div className="status-block">
        <div className="status-block-header">
          <strong>Detection status</strong>
          <span>{props.remediation.devicesWithResults} devices</span>
        </div>
        <div className="status-bar">
          <div className="status-bar-segment detection-pass" style={{ width: detectionPassWidth }} />
          <div className="status-bar-segment detection-fail" style={{ width: detectionFailWidth }} />
        </div>
        <div className="status-stats">
          <article className="status-stat">
            <span>Without issues</span>
            <strong>{props.remediation.passCount}</strong>
          </article>
          <article className="status-stat">
            <span>With issues</span>
            <strong>{props.remediation.failCount}</strong>
          </article>
        </div>
      </div>

      <div className="status-block">
        <div className="status-block-header">
          <strong>Remediation status</strong>
          <span>{props.remediation.platform}</span>
        </div>
        <div className="status-bar status-bar-muted">
          <div className="status-bar-segment remediation-fixed" style={{ width: remediationFixedWidth }} />
          <div className="status-bar-segment remediation-fail" style={{ width: remediationRemainingWidth }} />
        </div>
        <div className="status-stats">
          <article className="status-stat">
            <span>Issue fixed</span>
            <strong>{props.remediation.remediatedCount}</strong>
          </article>
          <article className="status-stat">
            <span>Still failing</span>
            <strong>{props.remediation.failCount}</strong>
          </article>
          <article className="status-stat">
            <span>Detected total</span>
            <strong>{props.remediation.passCount + props.remediation.failCount}</strong>
          </article>
        </div>
      </div>
    </section>
  );
}

function LoadingOverlay(props: { label: string }) {
  return (
    <div className="loading-overlay" role="status" aria-live="polite">
      <div className="loading-card">
        <div className="loading-spinner" />
        <strong>{props.label}</strong>
      </div>
    </div>
  );
}

function DebouncedInput(props: {
  value: string;
  placeholder?: string;
  delay?: number;
  onChange: (value: string) => void;
}) {
  const [draft, setDraft] = useState(props.value);

  useEffect(() => {
    setDraft(props.value);
  }, [props.value]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      if (draft !== props.value) {
        props.onChange(draft);
      }
    }, props.delay ?? 250);

    return () => window.clearTimeout(timeout);
  }, [draft, props.delay, props.onChange, props.value]);

  return <input value={draft} onChange={(event) => setDraft(event.target.value)} placeholder={props.placeholder} />;
}

function SummarySection(props: {
  title: string;
  collapsed: boolean;
  onToggle: () => void;
  children: ReactNode;
}) {
  return (
    <section className="summary-section">
      <button type="button" className="summary-toggle" onClick={props.onToggle} aria-expanded={!props.collapsed}>
        <span>{props.title}</span>
        <span className="summary-toggle-meta">
          <span className="summary-toggle-icon" aria-hidden="true">
            {props.collapsed ? "+" : "-"}
          </span>
          <span>{props.collapsed ? "Expand" : "Collapse"}</span>
        </span>
      </button>
      {!props.collapsed ? props.children : null}
    </section>
  );
}

const delimiterPresets: Array<{ key: DelimiterPreset; label: string; value: string }> = [
  { key: "comma", label: "Comma (,)", value: "," },
  { key: "semicolon", label: "Semicolon (;)", value: ";" },
  { key: "pipe", label: "Pipe (|)", value: "|" },
  { key: "colon", label: "Colon (:)", value: ":" },
  { key: "tab", label: "Tab", value: "\t" },
  { key: "custom", label: "Custom", value: "" },
];

const delimitedSourceColumnKeys = [
  "detectionOutputRaw",
  "remediationOutputRaw",
  "errorSummary",
  "errorCode",
  "scriptVersion",
  "dataSource",
];

const defaultDelimiterModalState: DelimiterModalState = {
  sourceColumnKey: delimitedSourceColumnKeys[0] ?? "detectionOutputRaw",
  preset: "comma",
  customDelimiter: "",
};

function getDelimiterValue(state: DelimiterModalState): string {
  if (state.preset === "custom") {
    return state.customDelimiter;
  }

  return delimiterPresets.find((preset) => preset.key === state.preset)?.value ?? ",";
}

function getDelimiterLabel(state: DelimiterModalState): string {
  if (state.preset === "custom") {
    return state.customDelimiter === "\t" ? "Tab" : state.customDelimiter;
  }

  return delimiterPresets.find((preset) => preset.key === state.preset)?.label ?? "Comma (,)";
}

function buildDerivedColumns(
  rows: JoinedResult[],
  sourceColumn: GridColumn,
  delimiterState: DelimiterModalState,
  existingDefinitions: DerivedColumnDefinition[],
): DerivedColumnDefinition[] {
  const delimiter = getDelimiterValue(delimiterState);
  if (!delimiter) {
    return [];
  }

  const maxParts = Math.max(
    0,
    ...rows.map((row) => {
      const value = getCellDisplayValue(row, sourceColumn.key);
      if (!value) {
        return 0;
      }

      return value.split(delimiter).length;
    }),
  );

  if (maxParts < 2) {
    return [];
  }

  const existingKeys = new Set(existingDefinitions.map((column) => column.key));

  return Array.from({ length: maxParts }, (_, index) => {
    let key = `${derivedColumnKeyPrefix}${sourceColumn.key}:${delimiterState.preset}:${index + 1}`;
    let counter = 1;
    while (existingKeys.has(key)) {
      counter++;
      key = `${derivedColumnKeyPrefix}${sourceColumn.key}:${delimiterState.preset}:${index + 1}:${counter}`;
    }
    existingKeys.add(key);

    return {
      key,
      label: `${sourceColumn.label} ${index + 1}`,
      sourceColumnKey: sourceColumn.key,
      delimiter,
      delimiterLabel: getDelimiterLabel(delimiterState),
      partIndex: index,
    };
  });
}

function DelimiterModal(props: {
  state: DelimiterModalState;
  sourceColumns: GridColumn[];
  previewRow: JoinedResult | null;
  onChange: (next: DelimiterModalState) => void;
  onApply: () => void;
  onClose: () => void;
}) {
  const selectedSourceColumn = props.sourceColumns.find((column) => column.key === props.state.sourceColumnKey) ?? props.sourceColumns[0];
  const delimiter = getDelimiterValue(props.state);
  const previewParts = props.previewRow && selectedSourceColumn && delimiter
    ? getCellDisplayValue(props.previewRow, selectedSourceColumn.key)
        .split(delimiter)
        .map((part) => part.trim())
        .filter((part, index, parts) => part.length > 0 || index < parts.length - 1)
    : [];
  const previewSummary = previewParts.length > 0
    ? `Sample row splits into ${previewParts.length} part${previewParts.length === 1 ? "" : "s"}. Example: ${previewParts.slice(0, 3).map((part) => part || "(empty)").join(" | ")}${previewParts.length > 3 ? "..." : ""}`
    : "Pick a delimiter that actually splits the selected value.";

  return (
    <div className="modal-backdrop" onClick={props.onClose}>
      <section className="card modal-card" onClick={(event) => event.stopPropagation()}>
        <header className="card-header">
          <div>
            <h3>Delimit output</h3>
            <p>Split a text output column into additional columns for review and export.</p>
          </div>
          <button type="button" className="icon-button" onClick={props.onClose}>
            Close
          </button>
        </header>
        <div className="modal-grid">
          <label className="field">
            <span>Source column</span>
            <select
              value={props.state.sourceColumnKey}
              onChange={(event) =>
                props.onChange({
                  ...props.state,
                  sourceColumnKey: event.target.value,
                })
              }
            >
              {props.sourceColumns.map((column) => (
                <option key={column.key} value={column.key}>
                  {column.sidebarLabel ?? column.label}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Delimiter</span>
            <select
              value={props.state.preset}
              onChange={(event) =>
                props.onChange({
                  ...props.state,
                  preset: event.target.value as DelimiterPreset,
                })
              }
            >
              {delimiterPresets.map((preset) => (
                <option key={preset.key} value={preset.key}>
                  {preset.label}
                </option>
              ))}
            </select>
          </label>
          {props.state.preset === "custom" ? (
            <label className="field modal-grid-span">
              <span>Custom delimiter</span>
              <input
                value={props.state.customDelimiter}
                onChange={(event) =>
                  props.onChange({
                    ...props.state,
                    customDelimiter: event.target.value,
                  })
                }
                placeholder="Enter delimiter text"
              />
            </label>
          ) : null}
        </div>
        <section className="delimiter-preview">
          <div className="split-meta">
            <strong>Preview</strong>
            <span>The final column count is calculated from the full filtered dataset when you apply the split.</span>
          </div>
          <p className="empty-state">{previewSummary}</p>
        </section>
        <div className="modal-actions">
          <button type="button" className="secondary" onClick={props.onClose}>
            Cancel
          </button>
          <button type="button" className="success" onClick={props.onApply} disabled={!delimiter}>
            Apply split
          </button>
        </div>
      </section>
    </div>
  );
}

function ResultsGrid(props: {
  columns: GridColumn[];
  rows: JoinedResult[];
  totalCount: number;
  page: number;
  pageSize: number;
  gridState: GridState;
  onGridChange: Dispatch<SetStateAction<GridState>>;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onSelectRow: (row: JoinedResult) => void;
  onExport: () => void;
  selectFilterOptions: Record<string, string[]>;
  onOpenSelectFilter: (columnKey: string) => void;
  toolbarExtra?: ReactNode;
}) {
  const visibleColumns = props.gridState.visibleColumns
    .map((columnKey) => props.columns.find((column) => column.key === columnKey))
    .filter((column): column is GridColumn => Boolean(column));
  const longTextColumns = new Set(["detectionOutputRaw", "remediationOutputRaw", "errorSummary"]);
  const [openFilterKey, setOpenFilterKey] = useState<string | null>(null);
  const [filterSearch, setFilterSearch] = useState<Record<string, string>>({});
  const [draftSelectFilters, setDraftSelectFilters] = useState<Record<string, string[]>>({});
  const [draggedColumnKey, setDraggedColumnKey] = useState<string | null>(null);
  const [dragOverColumnKey, setDragOverColumnKey] = useState<string | null>(null);
  const [tableScrollMetrics, setTableScrollMetrics] = useState({ scrollWidth: 0, clientWidth: 0, scrollLeft: 0 });
  const resizingColumnRef = useRef<{ columnKey: string; startX: number; startWidth: number } | null>(null);
  const tableWrapRef = useRef<HTMLDivElement | null>(null);
  const floatingScrollbarRef = useRef<HTMLDivElement | null>(null);
  const floatingScrollbarInnerRef = useRef<HTMLDivElement | null>(null);
  const syncingScrollRef = useRef<"table" | "floating" | null>(null);

  function getOptions(columnKey: string) {
    const providedOptions = props.selectFilterOptions[columnKey];
    const baseOptions = providedOptions && providedOptions.length > 0
      ? providedOptions
      : Array.from(new Set(props.rows.map((row) => getCellDisplayValue(row, columnKey, props.gridState.derivedColumns)).filter(Boolean))).sort((left, right) =>
          left.localeCompare(right),
        );

    const searchValue = filterSearch[columnKey]?.trim().toLowerCase();
    if (!searchValue) {
      return baseOptions;
    }

    return baseOptions.filter((option) => option.toLowerCase().includes(searchValue));
  }

  function getColumnWidth(columnKey: string, fallbackWidth: string) {
    const customWidth = props.gridState.columnWidths[columnKey];
    return customWidth ? `${customWidth}px` : fallbackWidth;
  }

  function toggleSelectFilter(columnKey: string, value: string) {
    const current = draftSelectFilters[columnKey] ?? props.gridState.columnFilters[columnKey] ?? [];
    const next = current.includes(value) ? current.filter((entry) => entry !== value) : [...current, value];
    setDraftSelectFilters((currentDrafts) => ({
      ...currentDrafts,
      [columnKey]: next,
    }));
  }

  function clearSelectFilter(columnKey: string) {
    setDraftSelectFilters((currentDrafts) => ({
      ...currentDrafts,
      [columnKey]: [],
    }));
  }

  function applySelectFilter(columnKey: string) {
    const nextValues = draftSelectFilters[columnKey] ?? props.gridState.columnFilters[columnKey] ?? [];
    props.onGridChange({
      ...props.gridState,
      columnFilters: {
        ...props.gridState.columnFilters,
        [columnKey]: nextValues,
      },
    });
    setOpenFilterKey(null);
  }

  function cancelSelectFilter(columnKey: string) {
    setDraftSelectFilters((currentDrafts) => ({
      ...currentDrafts,
      [columnKey]: props.gridState.columnFilters[columnKey] ?? [],
    }));
    setOpenFilterKey(null);
  }

  function reorderVisibleColumns(sourceKey: string, targetKey: string) {
    if (sourceKey === targetKey) {
      return;
    }

    const sourceIndex = props.gridState.visibleColumns.indexOf(sourceKey);
    const targetIndex = props.gridState.visibleColumns.indexOf(targetKey);
    if (sourceIndex === -1 || targetIndex === -1) {
      return;
    }

    const nextVisibleColumns = [...props.gridState.visibleColumns];
    const [movedColumn] = nextVisibleColumns.splice(sourceIndex, 1);
    if (!movedColumn) {
      return;
    }
    nextVisibleColumns.splice(targetIndex, 0, movedColumn);

    props.onGridChange({
      ...props.gridState,
      visibleColumns: nextVisibleColumns,
    });
  }

  function handleColumnDragStart(columnKey: string) {
    setDraggedColumnKey(columnKey);
    setDragOverColumnKey(columnKey);
  }

  function handleColumnDrop(targetKey: string) {
    if (draggedColumnKey) {
      reorderVisibleColumns(draggedColumnKey, targetKey);
    }

    setDraggedColumnKey(null);
    setDragOverColumnKey(null);
  }

  function handleColumnDragEnd() {
    setDraggedColumnKey(null);
    setDragOverColumnKey(null);
  }

  function startColumnResize(event: React.MouseEvent<HTMLSpanElement>, columnKey: string, width: string) {
    event.preventDefault();
    event.stopPropagation();

    resizingColumnRef.current = {
      columnKey,
      startX: event.clientX,
      startWidth: props.gridState.columnWidths[columnKey] ?? Number.parseInt(width, 10),
    };

    const onMouseMove = (moveEvent: MouseEvent) => {
      if (!resizingColumnRef.current) {
        return;
      }

      const nextWidth = Math.max(110, resizingColumnRef.current.startWidth + (moveEvent.clientX - resizingColumnRef.current.startX));
      props.onGridChange({
        ...props.gridState,
        columnWidths: {
          ...props.gridState.columnWidths,
          [columnKey]: nextWidth,
        },
      });
    };

    const onMouseUp = () => {
      resizingColumnRef.current = null;
      window.removeEventListener("mousemove", onMouseMove);
      window.removeEventListener("mouseup", onMouseUp);
    };

    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
  }

  function hideColumn(columnKey: string) {
    props.onGridChange((current) => ({
      ...current,
      visibleColumns: current.visibleColumns.filter((key) => key !== columnKey),
      columnFilters: Object.fromEntries(
        Object.entries(current.columnFilters).filter(([key]) => key !== columnKey),
      ),
      columnWidths: Object.fromEntries(
        Object.entries(current.columnWidths).filter(([key]) => key !== columnKey),
      ),
      sortKey: current.sortKey === columnKey ? defaultGridState.sortKey : current.sortKey,
      sortDirection: current.sortKey === columnKey ? defaultGridState.sortDirection : current.sortDirection,
    }));
  }

  useEffect(() => {
    function closeOnOutsideClick() {
      setOpenFilterKey(null);
    }

    if (!openFilterKey) {
      return undefined;
    }

    window.addEventListener("click", closeOnOutsideClick);
    return () => window.removeEventListener("click", closeOnOutsideClick);
  }, [openFilterKey]);

  useEffect(() => {
    function syncMetrics() {
      const tableWrap = tableWrapRef.current;
      if (!tableWrap) {
        return;
      }

      setTableScrollMetrics({
        scrollWidth: tableWrap.scrollWidth,
        clientWidth: tableWrap.clientWidth,
        scrollLeft: tableWrap.scrollLeft,
      });
    }

    syncMetrics();
    window.addEventListener("resize", syncMetrics);
    return () => window.removeEventListener("resize", syncMetrics);
  }, [props.rows, visibleColumns, props.gridState.columnWidths]);

  useEffect(() => {
    const tableWrap = tableWrapRef.current;
    const floatingScrollbar = floatingScrollbarRef.current;
    if (!tableWrap || !floatingScrollbar) {
      return undefined;
    }

    const activeTableWrap = tableWrap;
    const activeFloatingScrollbar = floatingScrollbar;

    function syncFromTable() {
      if (syncingScrollRef.current === "floating") {
        return;
      }

      syncingScrollRef.current = "table";
      activeFloatingScrollbar.scrollLeft = activeTableWrap.scrollLeft;
      setTableScrollMetrics({
        scrollWidth: activeTableWrap.scrollWidth,
        clientWidth: activeTableWrap.clientWidth,
        scrollLeft: activeTableWrap.scrollLeft,
      });
      syncingScrollRef.current = null;
    }

    function syncFromFloating() {
      if (syncingScrollRef.current === "table") {
        return;
      }

      syncingScrollRef.current = "floating";
      activeTableWrap.scrollLeft = activeFloatingScrollbar.scrollLeft;
      setTableScrollMetrics((current) => ({
        ...current,
        scrollLeft: activeFloatingScrollbar.scrollLeft,
      }));
      syncingScrollRef.current = null;
    }

    activeTableWrap.addEventListener("scroll", syncFromTable);
    activeFloatingScrollbar.addEventListener("scroll", syncFromFloating);
    syncFromTable();

    return () => {
      activeTableWrap.removeEventListener("scroll", syncFromTable);
      activeFloatingScrollbar.removeEventListener("scroll", syncFromFloating);
    };
  }, [props.rows, visibleColumns, props.gridState.columnWidths]);

  return (
    <section className="card grid-card">
      <header className="card-header">
        <h3>Results Grid</h3>
        <div className="toolbar">
          <span className="row-count-indicator">
            {props.totalCount.toLocaleString()} row{props.totalCount === 1 ? "" : "s"}
          </span>
          {props.toolbarExtra}
          <button
            className="secondary"
            onClick={() =>
              props.onGridChange({
                ...props.gridState,
                columnFilters: {},
              })
            }
          >
            Clear Column Filters
          </button>
          <button className="success" onClick={props.onExport}>Export CSV</button>
        </div>
      </header>
      <div className="table-wrap" ref={tableWrapRef}>
        <table>
          <thead>
            <tr>
              {visibleColumns.map((column) => (
                <th
                  key={column.key}
                  style={{ minWidth: getColumnWidth(column.key, column.width), width: getColumnWidth(column.key, column.width) }}
                  className={dragOverColumnKey === column.key ? "header-drop-target" : undefined}
                  onDragOver={(event) => {
                    if (!draggedColumnKey) {
                      return;
                    }

                    event.preventDefault();
                    if (dragOverColumnKey !== column.key) {
                      setDragOverColumnKey(column.key);
                    }
                  }}
                  onDrop={(event) => {
                    event.preventDefault();
                    handleColumnDrop(column.key);
                  }}
                  onClick={() =>
                    props.onGridChange({
                      ...props.gridState,
                      sortKey: column.key,
                      sortDirection:
                        props.gridState.sortKey === column.key && props.gridState.sortDirection === "asc" ? "desc" : "asc",
                    })
                  }
                >
                  <div className="header-plate">
                    <div className="header-plate-top">
                      <button
                        type="button"
                        className="header-hide-button"
                        title="Hide column"
                        onClick={(event) => {
                          event.stopPropagation();
                          hideColumn(column.key);
                        }}
                      >
                        Hide
                      </button>
                    </div>
                    <span className="header-plate-label">
                      {column.sidebarLabel ?? column.label}
                      {props.gridState.sortKey === column.key ? (props.gridState.sortDirection === "asc" ? " ^" : " v") : ""}
                    </span>
                  </div>
                  <span
                    className="column-drag-handle header-drag-handle"
                    draggable
                    title="Drag to reorder"
                    onDragStart={(event) => {
                      event.stopPropagation();
                      handleColumnDragStart(column.key);
                    }}
                    onDragEnd={(event) => {
                      event.stopPropagation();
                      handleColumnDragEnd();
                    }}
                    onClick={(event) => event.stopPropagation()}
                  >
                    ⋮⋮
                  </span>
                  <span className="column-resizer" onMouseDown={(event) => startColumnResize(event, column.key, column.width)} />
                </th>
              ))}
            </tr>
            <tr className="filter-row">
              {visibleColumns.map((column, columnIndex) => (
                <th
                  key={`${column.key}-filter`}
                  style={{ minWidth: getColumnWidth(column.key, column.width), width: getColumnWidth(column.key, column.width) }}
                >
                  {column.filter === "select" ? (
                    <div
                      className={
                        columnIndex >= visibleColumns.length - 2
                          ? "filter-menu-wrap filter-menu-wrap-end"
                          : "filter-menu-wrap"
                      }
                    >
                      <button
                        type="button"
                        className="filter-trigger"
                        title={
                          (props.gridState.columnFilters[column.key] ?? []).length > 0
                            ? `${(props.gridState.columnFilters[column.key] ?? []).length} selected`
                            : "All"
                        }
                        onClick={(event) => {
                          event.stopPropagation();
                          props.onOpenSelectFilter(column.key);
                          if (openFilterKey === column.key) {
                            cancelSelectFilter(column.key);
                            return;
                          }

                          setDraftSelectFilters((currentDrafts) => ({
                            ...currentDrafts,
                            [column.key]: props.gridState.columnFilters[column.key] ?? [],
                          }));
                          setOpenFilterKey(column.key);
                        }}
                      >
                        <span className="filter-trigger-label">
                          {(props.gridState.columnFilters[column.key] ?? []).length > 0
                            ? `${(props.gridState.columnFilters[column.key] ?? []).length} selected`
                            : "All"}
                        </span>
                        <span className="filter-trigger-caret" aria-hidden="true">
                          ▾
                        </span>
                      </button>
                      {openFilterKey === column.key ? (
                    <div className="filter-menu" onClick={(event) => event.stopPropagation()}>
                          <div className="filter-menu-actions">
                            <button type="button" className="secondary" onClick={() => clearSelectFilter(column.key)}>
                              Clear
                            </button>
                            <button type="button" className="secondary" onClick={() => cancelSelectFilter(column.key)}>
                              Cancel
                            </button>
                            <button type="button" className="success" onClick={() => applySelectFilter(column.key)}>
                              Apply
                            </button>
                          </div>
                          <input
                            className="filter-menu-search"
                            value={filterSearch[column.key] ?? ""}
                            onChange={(event) =>
                              setFilterSearch((current) => ({
                                ...current,
                                [column.key]: event.target.value,
                              }))
                            }
                            placeholder="Search values..."
                          />
                          <div className="filter-checkboxes">
                            {getOptions(column.key).map((option) => (
                              <label key={option} title={getDisplayValueForColumnValue(column.key, option)}>
                                <input
                                  type="checkbox"
                                  checked={(draftSelectFilters[column.key] ?? props.gridState.columnFilters[column.key] ?? []).includes(option)}
                                  onChange={() => toggleSelectFilter(column.key, option)}
                                />
                                <span>{getDisplayValueForColumnValue(column.key, option)}</span>
                              </label>
                            ))}
                          </div>
                        </div>
                      ) : null}
                    </div>
                  ) : (
                    <input
                      value={props.gridState.columnFilters[column.key]?.[0] ?? ""}
                      onClick={(event) => event.stopPropagation()}
                      onChange={(event) =>
                        props.onGridChange({
                          ...props.gridState,
                          columnFilters: {
                            ...props.gridState.columnFilters,
                            [column.key]: event.target.value ? [event.target.value] : [],
                          },
                        })
                      }
                      placeholder="Filter..."
                    />
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {props.rows.map((row) => (
              <tr key={row.resultId} onClick={() => props.onSelectRow(row)}>
                {visibleColumns.map((column) => (
                  <td
                    key={column.key}
                    style={{ minWidth: getColumnWidth(column.key, column.width), width: getColumnWidth(column.key, column.width) }}
                  >
                    {column.key === "status" ? <span className={`status-chip ${statusColors[row.status]}`}>{row.status}</span> : null}
                    {column.key !== "status"
                      ? longTextColumns.has(column.key)
                        ? <div className="cell-text clamp-two-lines">{getCellDisplayValue(row, column.key, props.gridState.derivedColumns)}</div>
                        : getCellDisplayValue(row, column.key, props.gridState.derivedColumns)
                      : null}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {tableScrollMetrics.scrollWidth > tableScrollMetrics.clientWidth ? (
        <div className="floating-scrollbar-shell">
          <div className="floating-scrollbar" ref={floatingScrollbarRef} aria-label="Horizontal table scrollbar">
            <div
              ref={floatingScrollbarInnerRef}
              style={{ width: `${tableScrollMetrics.scrollWidth}px` }}
              className="floating-scrollbar-inner"
            />
          </div>
        </div>
      ) : null}
      <footer className="grid-footer">
        <div className="grid-summary">
          {props.totalCount === 0
            ? "No matching rows"
            : `Showing ${(props.page - 1) * props.pageSize + 1}-${Math.min(props.page * props.pageSize, props.totalCount)} of ${props.totalCount}`}
        </div>
        <div className="pager">
          <label>
            Page size
            <select value={props.pageSize} onChange={(event) => props.onPageSizeChange(Number(event.target.value))}>
              {[25, 50, 100, 250].map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </label>
          <button className="secondary" onClick={() => props.onPageChange(props.page - 1)} disabled={props.page <= 1}>
            Previous
          </button>
          <span className="page-indicator">Page {props.page}</span>
          <button
            className="secondary"
            onClick={() => props.onPageChange(props.page + 1)}
            disabled={props.page * props.pageSize >= props.totalCount}
          >
            Next
          </button>
        </div>
      </footer>
    </section>
  );
}

function ColumnManager(props: {
  columns: GridColumn[];
  gridState: GridState;
  onGridChange: Dispatch<SetStateAction<GridState>>;
}) {
  const [editingDerivedKey, setEditingDerivedKey] = useState<string | null>(null);
  const [editingDerivedLabel, setEditingDerivedLabel] = useState("");
  const orderedColumnKeys = [
    ...props.gridState.visibleColumns,
    ...props.columns
      .map((column) => column.key)
      .filter((columnKey) => !props.gridState.visibleColumns.includes(columnKey)),
  ];

  function startRename(column: GridColumn) {
    setEditingDerivedKey(column.key);
    setEditingDerivedLabel(column.sidebarLabel ?? column.label);
  }

  function commitRename() {
    if (!editingDerivedKey) {
      return;
    }

    const nextLabel = editingDerivedLabel.trim();
    if (!nextLabel) {
      setEditingDerivedKey(null);
      setEditingDerivedLabel("");
      return;
    }

    props.onGridChange((current) => ({
      ...current,
      derivedColumns: current.derivedColumns.map((column) =>
        column.key === editingDerivedKey
          ? {
              ...column,
              label: nextLabel,
            }
          : column,
      ),
    }));

    setEditingDerivedKey(null);
    setEditingDerivedLabel("");
  }

  function removeDerivedColumn(columnKey: string) {
    props.onGridChange((current) => {
      const keysToRemove = new Set<string>();
      const queue = [columnKey];

      while (queue.length > 0) {
        const nextKey = queue.shift();
        if (!nextKey || keysToRemove.has(nextKey)) {
          continue;
        }

        keysToRemove.add(nextKey);
        current.derivedColumns
          .filter((column) => column.sourceColumnKey === nextKey)
          .forEach((column) => queue.push(column.key));
      }

      const nextDerivedColumns = current.derivedColumns.filter((column) => !keysToRemove.has(column.key));
      const nextVisibleColumns = current.visibleColumns.filter((key) => !keysToRemove.has(key));
      const nextColumnFilters = Object.fromEntries(
        Object.entries(current.columnFilters).filter(([key]) => !keysToRemove.has(key)),
      );
      const nextColumnWidths = Object.fromEntries(
        Object.entries(current.columnWidths).filter(([key]) => !keysToRemove.has(key)),
      );

      return {
        ...current,
        derivedColumns: nextDerivedColumns,
        visibleColumns: nextVisibleColumns,
        columnFilters: nextColumnFilters,
        columnWidths: nextColumnWidths,
        sortKey: keysToRemove.has(current.sortKey) ? defaultGridState.sortKey : current.sortKey,
        sortDirection: keysToRemove.has(current.sortKey) ? defaultGridState.sortDirection : current.sortDirection,
      };
    });

    if (editingDerivedKey && (editingDerivedKey === columnKey)) {
      setEditingDerivedKey(null);
      setEditingDerivedLabel("");
    }
  }

  function renderColumn(columnKey: string) {
    const column = props.columns.find((entry) => entry.key === columnKey);
    if (!column) {
      return null;
    }

    const visible = props.gridState.visibleColumns.includes(column.key);

    return (
      <div
        key={column.key}
        className={visible ? "column-picker-item is-visible" : "column-picker-item"}
      >
        <div className="column-picker-controls">
          <button
            type="button"
            className={visible ? "column-visibility-button is-active" : "column-visibility-button is-inactive"}
            onClick={() =>
              props.onGridChange({
                ...props.gridState,
                visibleColumns: visible
                  ? props.gridState.visibleColumns.filter((entry) => entry !== column.key)
                  : [...props.gridState.visibleColumns, column.key],
              })
            }
            aria-pressed={visible}
          >
            {visible ? "Active" : "Inactive"}
          </button>
          {column.derived ? (
            <div className="column-derived-actions">
              <button
                type="button"
                className="column-rename-button secondary"
                onClick={() => startRename(column)}
                aria-label={`Rename ${column.label}`}
              >
                Rename
              </button>
              <button
                type="button"
                className="column-remove-button secondary"
                onClick={() => removeDerivedColumn(column.key)}
                aria-label={`Remove ${column.label}`}
              >
                Remove
              </button>
            </div>
          ) : null}
        </div>
        {column.derived && editingDerivedKey === column.key ? (
          <input
            className="column-rename-input"
            value={editingDerivedLabel}
            onChange={(event) => setEditingDerivedLabel(event.target.value)}
            onBlur={commitRename}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                commitRename();
              }
              if (event.key === "Escape") {
                setEditingDerivedKey(null);
                setEditingDerivedLabel("");
              }
            }}
            autoFocus
          />
        ) : (
          <span className="column-picker-label">{column.sidebarLabel ?? column.label}</span>
        )}
      </div>
    );
  }

  return (
    <section className="sidebar-panel">
      <header className="sidebar-panel-header">
        <strong>Columns</strong>
        <div className="sidebar-panel-actions">
          <span>{props.gridState.visibleColumns.length} visible</span>
        </div>
      </header>
      <p className="sidebar-panel-copy">Toggle columns here. Reorder them from the table headers.</p>
      <div className="sidebar-column-list flat-column-list">
        {orderedColumnKeys.map((columnKey) => renderColumn(columnKey))}
      </div>
    </section>
  );
}

function normalizeGridState(gridState: Partial<GridState> | null | undefined): GridState {
  return {
    visibleColumns: Array.isArray(gridState?.visibleColumns) && gridState.visibleColumns.length > 0
      ? gridState.visibleColumns
      : defaultGridState.visibleColumns,
    sortKey: typeof gridState?.sortKey === "string" ? gridState.sortKey : defaultGridState.sortKey,
    sortDirection: gridState?.sortDirection === "asc" || gridState?.sortDirection === "desc"
      ? gridState.sortDirection
      : defaultGridState.sortDirection,
    columnFilters:
      gridState && typeof gridState.columnFilters === "object" && gridState.columnFilters
        ? gridState.columnFilters
        : defaultGridState.columnFilters,
    columnWidths:
      gridState && typeof gridState.columnWidths === "object" && gridState.columnWidths
        ? gridState.columnWidths
        : defaultGridState.columnWidths,
    derivedColumns: Array.isArray(gridState?.derivedColumns) ? gridState.derivedColumns : defaultGridState.derivedColumns,
  };
}

function loadWorkspaceState(): WorkspaceState | null {
  const raw = window.localStorage.getItem(workspaceStateStorageKey);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<WorkspaceState>;
    if (!parsed || typeof parsed !== "object") {
      return null;
    }

    return {
      activePage: "remediations",
      filters: parsed.filters ?? defaultFilters,
      gridState: normalizeGridState(parsed.gridState),
      pageSize: typeof parsed.pageSize === "number" ? parsed.pageSize : 50,
    };
  } catch {
    return null;
  }
}

async function retryAsync<T>(work: () => Promise<T>, attempts = 3, delayMs = 1200): Promise<T> {
  let lastError: unknown;

  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return await work();
    } catch (caughtError) {
      lastError = caughtError;
      if (attempt < attempts) {
        await new Promise((resolve) => window.setTimeout(resolve, delayMs));
      }
    }
  }

  throw lastError;
}

export function App() {
  const initialWorkspaceState = loadWorkspaceState();
  const [filters, setFilters] = useState<FiltersState>(initialWorkspaceState?.filters ?? defaultFilters);
  const [gridState, setGridState] = useState<GridState>(initialWorkspaceState?.gridState ?? defaultGridState);
  const [selectedRow, setSelectedRow] = useState<JoinedResult | null>(null);
  const [remediations, setRemediations] = useState<Remediation[]>([]);
  const [resultsPage, setResultsPage] = useState<PagedResults>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 50,
  });
  const [imports, setImports] = useState<ImportBatch[]>([]);
  const [selectedImport, setSelectedImport] = useState<ImportBatch | null>(null);
  const [importFiles, setImportFiles] = useState<File[]>([]);
  const [importQueue, setImportQueue] = useState<ImportQueueItem[]>([]);
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [resettingData, setResettingData] = useState(false);
  const [loading, setLoading] = useState(true);
  const [resultsLoading, setResultsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialWorkspaceState?.pageSize ?? 50);
  const [selectFilterOptions, setSelectFilterOptions] = useState<Record<string, string[]>>({});
  const [importedColumnLabels, setImportedColumnLabels] = useState<Record<string, string>>({});
  const [isDragOverImport, setIsDragOverImport] = useState(false);
  const [collapsedSummaries, setCollapsedSummaries] = useState<Record<string, boolean>>({ remediations: false });
  const [toastVisible, setToastVisible] = useState(false);
  const [toastHovered, setToastHovered] = useState(false);
  const [dismissedToastKey, setDismissedToastKey] = useState<string | null>(null);
  const [delimiterModal, setDelimiterModal] = useState<DelimiterModalState | null>(null);
  const [delimiting, setDelimiting] = useState(false);
  const hadActiveImportsRef = useRef(false);
  const allColumns = useMemo(
    () =>
      buildAllGridColumns(
        gridColumns.map((column) => ({
          ...column,
          sidebarLabel: importedColumnLabels[column.key] ?? column.sidebarLabel ?? column.label,
        })),
        gridState.derivedColumns,
      ),
    [gridState.derivedColumns, importedColumnLabels],
  );
  const delimitedSourceColumns = allColumns;
  const displayRows = useMemo(() => {
    let nextRows = [...resultsPage.items];

    const derivedFilters = Object.entries(gridState.columnFilters).filter(
      ([columnKey, values]) => isDerivedColumnKey(columnKey) && values.some((value) => value.trim() !== ""),
    );

    if (derivedFilters.length > 0) {
      nextRows = nextRows.filter((row) =>
        derivedFilters.every(([columnKey, values]) =>
          getCellDisplayValue(row, columnKey, gridState.derivedColumns)
            .toLowerCase()
            .includes((values[0] ?? "").trim().toLowerCase()),
        ),
      );
    }

    if (isDerivedColumnKey(gridState.sortKey)) {
      nextRows.sort((left, right) => {
        const leftValue = getCellDisplayValue(left, gridState.sortKey, gridState.derivedColumns);
        const rightValue = getCellDisplayValue(right, gridState.sortKey, gridState.derivedColumns);
        const comparison = leftValue.localeCompare(rightValue);
        return gridState.sortDirection === "asc" ? comparison : -comparison;
      });
    }

    return nextRows;
  }, [gridState.columnFilters, gridState.derivedColumns, gridState.sortDirection, gridState.sortKey, resultsPage.items]);

  function clearWorkspaceState() {
    setRemediations([]);
    setResultsPage({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize,
    });
    setImports([]);
    setSelectedRow(null);
    setSelectedImport(null);
    setImportFiles([]);
    setImportQueue([]);
    setFilters(defaultFilters);
    setGridState(defaultGridState);
    setPage(1);
    setSelectFilterOptions({});
    setImportedColumnLabels({});
    hadActiveImportsRef.current = false;
  }

  function queueImportFiles(files: File[]) {
    const nextFiles = files.filter((file) => {
      const lowerName = file.name.toLowerCase();
      return lowerName.endsWith(".csv") || lowerName.endsWith(".zip");
    });
    if (nextFiles.length === 0) {
      return;
    }

    setImportFiles((current) => {
      const merged = [...current];
      for (const file of nextFiles) {
        if (!merged.some((existing) => getImportFileKey(existing) === getImportFileKey(file))) {
          merged.push(file);
        }
      }
      return merged;
    });

    setImportQueue((current) => {
      const preserved = current.filter((item) => item.status !== "selected");
      const nextItems = nextFiles.map((file) => ({
        key: getImportFileKey(file),
        name: file.name,
        status: "selected" as const,
        message: "Ready",
      }));
      const combined = [...preserved];
      for (const item of nextItems) {
        if (!combined.some((existing) => existing.key === item.key)) {
          combined.push(item);
        }
      }
      return combined;
    });
    setImportMessage(`${nextFiles.length} additional file${nextFiles.length === 1 ? "" : "s"} selected.`);
  }

  function buildResultsPayload(includePaging: boolean): ResultsQueryPayload {
    const columnFilters: Record<string, string[]> = {};
    Object.entries(gridState.columnFilters).forEach(([key, values]) => {
      if (isDerivedColumnKey(key)) {
        return;
      }

      const normalizedValues = values.map((value) => value.trim()).filter(Boolean);
      if (normalizedValues.length > 0) {
        columnFilters[key] = normalizedValues;
      }
    });

    return {
      ...(filters.search.trim() ? { search: filters.search.trim() } : {}),
      ...(filters.statuses[0] ? { status: filters.statuses[0] } : {}),
      ...(filters.selectedRemediationIds[0] ? { remediationId: filters.selectedRemediationIds[0] } : {}),
      ...(filters.selectedDeviceIds[0] ? { deviceId: filters.selectedDeviceIds[0] } : {}),
      ...(filters.models[0] ? { model: filters.models[0] } : {}),
      sortKey: isDerivedColumnKey(gridState.sortKey) ? defaultGridState.sortKey : gridState.sortKey,
      sortDirection: gridState.sortDirection,
      columnFilters,
      ...(includePaging
        ? {
            page,
            pageSize,
          }
        : {}),
    };
  }

  async function refreshResultsData() {
    const nextResults = await api.getResults(buildResultsPayload(true));
    setResultsPage(nextResults);
  }

  async function refreshWorkspaceData() {
    const [nextRemediations, nextImports, nextColumnLabels] = await Promise.allSettled([
      api.getRemediations(),
      api.getImports(),
      api.getColumnLabels(),
    ]);
    if (nextRemediations.status === "fulfilled") {
      setRemediations(nextRemediations.value);
    }
    if (nextImports.status === "fulfilled") {
      setImports(nextImports.value);
    }
    if (nextColumnLabels.status === "fulfilled") {
      setImportedColumnLabels(nextColumnLabels.value);
    }
  }

  useEffect(() => {
    async function loadReferenceData() {
      setLoading(true);
      setError(null);

      try {
        await refreshWorkspaceData();
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : "Failed to load data.");
      } finally {
        setLoading(false);
      }
    }

    void loadReferenceData();
  }, []);

  useEffect(() => {
    const hasActiveImports = imports.some((batch) => batch.status === "Queued" || batch.status === "Running");
    if (hadActiveImportsRef.current && !hasActiveImports) {
      hadActiveImportsRef.current = false;
      void (async () => {
        setImportMessage("Refreshing imported data...");
        try {
          await refreshWorkspaceData();
          await retryAsync(() => refreshResultsData(), 4, 1500);
          setError(null);
          setImportMessage("Import processing completed.");
        } catch (caughtError) {
          setError(caughtError instanceof Error ? caughtError.message : "Failed to refresh imported data.");
          setImportMessage("Import finished, but the table refresh needs one more try.");
        }
      })();
      return undefined;
    }

    if (!hasActiveImports) {
      return undefined;
    }

    hadActiveImportsRef.current = true;

    const interval = window.setInterval(() => {
      void refreshImports();
    }, 3000);

    return () => window.clearInterval(interval);
  }, [imports]);

  useEffect(() => {
    setPage(1);
    setSelectFilterOptions({});
  }, [filters, gridState.sortKey, gridState.sortDirection, gridState.columnFilters]);

  useEffect(() => {
    const snapshot: WorkspaceState = {
      activePage: "remediations" as PageId,
      filters,
      gridState,
      pageSize,
    };

    window.localStorage.setItem(workspaceStateStorageKey, JSON.stringify(snapshot));
  }, [filters, gridState, pageSize]);

  const activeToast = error
    ? { kind: "error" as const, message: error }
    : importMessage
      ? { kind: "info" as const, message: importMessage }
      : null;
  const activeToastKey = activeToast ? `${activeToast.kind}:${activeToast.message}` : null;

  useEffect(() => {
    if (!activeToast) {
      setToastVisible(false);
      setToastHovered(false);
      setDismissedToastKey(null);
      return;
    }

    if (dismissedToastKey === activeToastKey) {
      setToastVisible(false);
      return;
    }

    setToastVisible(true);
  }, [activeToast, activeToastKey, dismissedToastKey]);

  useEffect(() => {
    if (!activeToast || !toastVisible || toastHovered) {
      return undefined;
    }

    const timeout = window.setTimeout(() => {
      setToastVisible(false);
      if (activeToastKey) {
        setDismissedToastKey(activeToastKey);
      }
    }, 3000);

    return () => window.clearTimeout(timeout);
  }, [activeToast, activeToastKey, toastHovered, toastVisible]);

  useEffect(() => {
    async function loadResults() {
      setResultsLoading(true);
      try {
        const nextResults = await api.getResults(buildResultsPayload(true));
        setResultsPage(nextResults);
        setError(null);
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : "Failed to load results.");
      } finally {
        setResultsLoading(false);
      }
    }

    void loadResults();
  }, [filters, gridState.sortKey, gridState.sortDirection, gridState.columnFilters, page, pageSize]);
  const processingImports = imports.some((batch) => batch.status === "Queued" || batch.status === "Running");
  const loadingLabel = importing
    ? "Importing CSV..."
    : processingImports
      ? "Processing imported files..."
    : delimiting
      ? "Delimiting output..."
    : resettingData
        ? "Clearing database..."
      : loading
        ? "Loading workspace..."
        : resultsLoading
          ? "Loading results..."
          : null;

  async function refreshImports() {
    const nextImports = await api.getImports();
    setImports(nextImports);
  }

  async function loadSelectFilterOptions(columnKey: string) {
    if (isDerivedColumnKey(columnKey)) {
      return;
    }

    if (selectFilterOptions[columnKey]) {
      return;
    }

    const options = await api.getResultFilterOptions(columnKey, buildResultsPayload(false));
    setSelectFilterOptions((current) => ({
      ...current,
      [columnKey]: options,
    }));
  }

  async function getFullFilteredRows(): Promise<JoinedResult[]> {
    const exportPage = await api.getResults({
      ...buildResultsPayload(true),
      page: 1,
      pageSize: Math.max(resultsPage.totalCount, pageSize, 1),
    });
    let nextRows = [...exportPage.items];

    const derivedFilters = Object.entries(gridState.columnFilters).filter(
      ([columnKey, values]) => isDerivedColumnKey(columnKey) && values.some((value) => value.trim() !== ""),
    );

    if (derivedFilters.length > 0) {
      nextRows = nextRows.filter((row) =>
        derivedFilters.every(([columnKey, values]) =>
          getCellDisplayValue(row, columnKey, gridState.derivedColumns)
            .toLowerCase()
            .includes((values[0] ?? "").trim().toLowerCase()),
        ),
      );
    }

    if (isDerivedColumnKey(gridState.sortKey)) {
      nextRows.sort((left, right) => {
        const leftValue = getCellDisplayValue(left, gridState.sortKey, gridState.derivedColumns);
        const rightValue = getCellDisplayValue(right, gridState.sortKey, gridState.derivedColumns);
        const comparison = leftValue.localeCompare(rightValue);
        return gridState.sortDirection === "asc" ? comparison : -comparison;
      });
    }

    return nextRows;
  }

  async function exportCurrentResults() {
    if (gridState.derivedColumns.length === 0) {
      await api.exportResults({
        ...buildResultsPayload(false),
        visibleColumns: gridState.visibleColumns,
      });
      return;
    }

    try {
      setImportMessage("Preparing export...");
      const exportRows = await getFullFilteredRows();
      downloadGridCsv(exportRows, allColumns, gridState);
      setImportMessage("Export ready.");
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : "Failed to export results.");
    }
  }

  function getImportFileKey(file: File) {
    return `${file.name}-${file.size}-${file.lastModified}`;
  }

  function removeImportFile(fileKey: string) {
    setImportFiles((current) => current.filter((file) => getImportFileKey(file) !== fileKey));
    setImportQueue((current) => current.filter((item) => item.key !== fileKey));
  }

  function clearSelectedImportFiles() {
    setImportFiles([]);
    setImportQueue((current) => current.filter((item) => item.status !== "selected"));
  }

  async function importCsv() {
    if (importFiles.length === 0) {
      setImportMessage("Select one or more CSV files first.");
      return;
    }

    const filesToImport = [...importFiles];
    setImporting(true);
    setImportMessage(`Uploading ${filesToImport.length} file${filesToImport.length === 1 ? "" : "s"}...`);

    const batches: ImportBatch[] = [];
    let acceptedFileCount = 0;
    const failures: string[] = [];

    try {
      for (const [index, file] of filesToImport.entries()) {
        const fileKey = getImportFileKey(file);
        setImportMessage(`Uploading ${index + 1} of ${filesToImport.length}: ${file.name}`);
        setImportQueue((current) =>
          current.map((item) =>
            item.key === fileKey
              ? {
                  ...item,
                  status: "uploading",
                  message: "Uploading...",
                }
              : item,
          ),
        );

        try {
          const nextBatches = await api.uploadResultsCsv(file);
          batches.push(...nextBatches);
          acceptedFileCount++;
          setImportQueue((current) =>
            current.map((item) =>
              item.key === fileKey
                ? {
                    ...item,
                    status: "accepted",
                    message: nextBatches.length > 1 ? `${nextBatches.length} CSVs queued` : nextBatches[0]?.status ?? "Accepted",
                  }
                : item,
            ),
          );
        } catch (caughtError) {
          const failureMessage = caughtError instanceof Error ? caughtError.message : "Import failed.";
          failures.push(`${file.name}: ${failureMessage}`);
          setImportQueue((current) =>
            current.map((item) =>
              item.key === fileKey
                ? {
                    ...item,
                    status: "failed",
                    message: failureMessage,
                  }
                : item,
            ),
          );
        }
      }

      const queuedCount = batches.filter((batch) => batch.status === "Queued").length;

      if (acceptedFileCount > 0 && failures.length === 0) {
        setImportMessage(
          queuedCount > 0
            ? `${queuedCount} file${queuedCount === 1 ? "" : "s"} queued for background processing.`
            : batches.map((batch) => batch.message).join(" "),
        );
      } else if (acceptedFileCount > 0) {
        setImportMessage(
          `${acceptedFileCount} of ${filesToImport.length} file${filesToImport.length === 1 ? "" : "s"} accepted. ${failures.join(" | ")}`,
        );
      } else {
        setImportMessage(failures.join(" | ") || "Import failed.");
      }

      setImportFiles([]);
      setSelectedImport(null);
      await refreshImports();
    } finally {
      setImporting(false);
    }
  }

  async function openImport(importBatchId: string) {
    const batch = await api.getImport(importBatchId);
    setSelectedRow(null);
    setSelectedImport(batch);
  }

  async function resetWorkspaceData() {
    if (!window.confirm("Clear all imported data, saved views, and import history from the database?")) {
      return;
    }

    setResettingData(true);
    setImportMessage(null);
    setError(null);
    clearWorkspaceState();

    try {
      await api.resetData();
      await refreshWorkspaceData();
      setImportMessage("Database cleared.");
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : "Failed to clear database.");
    } finally {
      setResettingData(false);
    }
  }

  async function uploadMoreFiles(event: ChangeEvent<HTMLInputElement>) {
    queueImportFiles(Array.from(event.target.files ?? []));
    event.target.value = "";
  }

  function openHelpWindow() {
    window.open("/help.html", "inremedy-help", "noopener,noreferrer,width=1240,height=900");
  }

  function openDelimiterModal() {
    setDelimiterModal({
      ...defaultDelimiterModalState,
      sourceColumnKey: allColumns[0]?.key ?? defaultDelimiterModalState.sourceColumnKey,
    });
  }

  function applyDelimitedColumns() {
    if (!delimiterModal) {
      return;
    }

    const sourceColumn = gridColumns.find((column) => column.key === delimiterModal.sourceColumnKey);
    if (!sourceColumn) {
      setError("Choose a valid source column to split.");
      return;
    }

    void (async () => {
      setDelimiting(true);
      try {
        const rowsForSplit = await getFullFilteredRows();
        const nextDerivedColumns = buildDerivedColumns(rowsForSplit, sourceColumn, delimiterModal, gridState.derivedColumns);
        if (nextDerivedColumns.length === 0) {
          setImportMessage("No additional columns were created. Try another delimiter.");
          return;
        }

        setGridState((current) => ({
          ...current,
          derivedColumns: [...current.derivedColumns, ...nextDerivedColumns],
          visibleColumns: [...current.visibleColumns, ...nextDerivedColumns.map((column) => column.key)],
        }));
        setDelimiterModal(null);
        setImportMessage(`${nextDerivedColumns.length} delimited column${nextDerivedColumns.length === 1 ? "" : "s"} added.`);
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : "Failed to delimit output.");
      } finally {
        setDelimiting(false);
      }
    })();
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-content">
          <div className="brand-block">
            <img className="brand-logo" src={logoImage} alt="In-Remedy" />
            <p>Intune Remediation Explorer</p>
          </div>
          <ColumnManager columns={allColumns} gridState={gridState} onGridChange={setGridState} />
        </div>
        <div className="sidebar-footer">
          <button type="button" className="sidebar-help-button secondary" onClick={openHelpWindow}>
            Help
          </button>
        </div>
      </aside>
      <main className="main-area">
        {loadingLabel ? <LoadingOverlay label={loadingLabel} /> : null}
        {activeToast && toastVisible ? (
          <div className="toast-layer" aria-live="polite" aria-atomic="true">
            <section
              className={`toast-notification toast-${activeToast.kind}`}
              onMouseEnter={() => setToastHovered(true)}
              onMouseLeave={() => setToastHovered(false)}
            >
              <div className="toast-content">
                <strong>{activeToast.kind === "error" ? "Error" : "Notification"}</strong>
                <p>{activeToast.message}</p>
              </div>
              <button
                type="button"
                className="toast-close"
                onClick={() => {
                  setToastVisible(false);
                  if (activeToastKey) {
                    setDismissedToastKey(activeToastKey);
                  }
                }}
                aria-label="Dismiss notification"
              >
                x
              </button>
            </section>
          </div>
        ) : null}
        <header className="hero">
          <div>
            <span className="eyebrow">Operational Workspace</span>
            <h1>Remediations</h1>
            <p>Import your Intune remediation files, review the results, and shape the table into the exact view you want to export.</p>
          </div>
          <div
            className={isDragOverImport ? "hero-actions import-actions import-dropzone drag-over" : "hero-actions import-actions import-dropzone"}
            onDragOver={(event) => {
              event.preventDefault();
              setIsDragOverImport(true);
            }}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
                setIsDragOverImport(false);
              }
            }}
            onDrop={(event) => {
              event.preventDefault();
              setIsDragOverImport(false);
              queueImportFiles(Array.from(event.dataTransfer.files ?? []));
            }}
          >
              <div className="dropzone-copy compact">
                <strong>Drop CSV/ZIP</strong>
              </div>
              <div className="import-inline-controls">
                <input type="file" accept=".csv,.zip" multiple onChange={(event) => void uploadMoreFiles(event)} />
                <button className="success" onClick={() => void importCsv()} disabled={importing || importFiles.length === 0}>
                  {importing ? "Importing..." : "Import"}
                </button>
                <button className="danger" onClick={() => void resetWorkspaceData()} disabled={resettingData || importing}>
                  {resettingData ? "Clearing..." : "Clear Database"}
                </button>
              {importQueue.length > 0 ? (
              <div className="selected-files compact-files">
                {importQueue.map((item) => (
                  <div key={item.key} className={`file-chip file-chip-${item.status}`}>
                    {item.status === "uploading" ? <div className="mini-spinner" aria-hidden="true" /> : null}
                    <div>
                      <strong>{item.name}</strong>
                      <span>{item.message}</span>
                    </div>
                    {item.status === "selected" ? (
                      <button className="chip-action" onClick={() => removeImportFile(item.key)} aria-label={`Remove ${item.name}`}>
                        x
                      </button>
                    ) : null}
                  </div>
                ))}
              </div>
            ) : null}
            </div>
          </div>
        </header>
        {loading ? <section className="card">Loading backend data...</section> : null}

        <SummarySection
          title="Remediation Summary"
          collapsed={collapsedSummaries.remediations ?? false}
          onToggle={() =>
            setCollapsedSummaries((current) => ({
              ...current,
              remediations: !current.remediations,
            }))
          }
        >
          <section className="page-grid two-up">
            {remediations.map((remediation) => (
              <RemediationStatusCard key={remediation.remediationId} remediation={remediation} />
            ))}
          </section>
        </SummarySection>
        <ResultsGrid
          columns={allColumns}
          rows={displayRows}
          totalCount={resultsPage.totalCount}
          page={page}
          pageSize={pageSize}
          gridState={gridState}
          onGridChange={setGridState}
          onPageChange={setPage}
          onPageSizeChange={(nextPageSize) => {
            setPageSize(nextPageSize);
            setPage(1);
          }}
          onSelectRow={(row) => {
            setSelectedImport(null);
            setSelectedRow(row);
          }}
          onExport={exportCurrentResults}
          selectFilterOptions={selectFilterOptions}
          onOpenSelectFilter={(columnKey) => void loadSelectFilterOptions(columnKey)}
          toolbarExtra={
            <button type="button" className="secondary" onClick={openDelimiterModal}>
              Delimit output
            </button>
          }
        />

      </main>

      {selectedRow ? (
        <aside className="detail-drawer">
          <header className="card-header">
            <h3>{selectedRow.deviceName}</h3>
            <button className="icon-button" onClick={() => setSelectedRow(null)}>
              Close
            </button>
          </header>
          <div className="drawer-section">
            <div className="eyebrow">{selectedRow.remediationName}</div>
            <span className={`status-chip ${statusColors[selectedRow.status]}`}>{selectedRow.status}</span>
            <p>{formatUtc(selectedRow.runTimestampUtc)}</p>
          </div>
          <div className="drawer-section">
            <h4>Detection Output</h4>
            <pre>{selectedRow.detectionOutputRaw}</pre>
          </div>
          <div className="drawer-section">
            <h4>Remediation Output</h4>
            <pre>{selectedRow.remediationOutputRaw}</pre>
          </div>
          <div className="drawer-section">
            <h4>Metadata</h4>
            <p>Remediation Category: {selectedRow.remediationCategory}</p>
            <p>Platform: {selectedRow.platform}</p>
            <p>Manufacturer: {selectedRow.manufacturer}</p>
            <p>Category: {selectedRow.outputCategory}</p>
            <p>Model: {selectedRow.model}</p>
            <p>OS Version: {selectedRow.osVersion}</p>
            <p>OS Build: {selectedRow.osBuild}</p>
            <p>Region: {selectedRow.region}</p>
            <p>Update Ring: {selectedRow.updateRing}</p>
            <p>Detection Script Version: {selectedRow.detectionScriptVersion}</p>
            <p>Remediation Script Version: {selectedRow.remediationScriptVersion}</p>
            <p>Script Version: {selectedRow.scriptVersion}</p>
            <p>Data Source: {selectedRow.dataSource}</p>
            {selectedRow.errorSummary ? <p>Error Summary: {selectedRow.errorSummary}</p> : null}
          </div>
        </aside>
      ) : null}

      {selectedImport ? (
        <aside className="detail-drawer">
          <header className="card-header">
            <h3>{selectedImport.fileName}</h3>
            <button className="icon-button" onClick={() => setSelectedImport(null)}>
              Close
            </button>
          </header>
          <div className="drawer-section">
            <div className="eyebrow">{selectedImport.importType}</div>
            <p>{selectedImport.status}</p>
            <p>{selectedImport.message}</p>
            {selectedImport.duplicateOfImportBatchId ? <p>Duplicate of batch {selectedImport.duplicateOfImportBatchId}</p> : null}
            <p>
              Processed {selectedImport.processedRows} of {selectedImport.totalRows} rows. Imported: {selectedImport.importedRows}. Errors: {selectedImport.errorRows}
            </p>
          </div>
          <div className="drawer-section">
            <h4>Validation Errors</h4>
            {selectedImport.errors.length === 0 ? <p>No row-level validation errors.</p> : null}
            <div className="import-errors">
              {selectedImport.errors.map((importError) => (
                <pre key={importError.importErrorId}>
                  Row {importError.rowNumber} | {importError.columnName}
                  {"\n"}
                  {importError.errorMessage}
                  {"\n\n"}
                  {importError.rowSnapshotJson}
                </pre>
              ))}
            </div>
          </div>
        </aside>
      ) : null}

      {delimiterModal ? (
        <DelimiterModal
          state={delimiterModal}
          sourceColumns={delimitedSourceColumns}
          previewRow={resultsPage.items[0] ?? null}
          onChange={setDelimiterModal}
          onApply={applyDelimitedColumns}
          onClose={() => setDelimiterModal(null)}
        />
      ) : null}
    </div>
  );
}
