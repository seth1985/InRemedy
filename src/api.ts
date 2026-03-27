import type {
  ImportBatch,
  ImportPreview,
  PagedResults,
  Remediation,
  GridState,
  SavedView,
  SavedViewDefinition,
  FiltersState,
} from "./types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "/api";

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export interface ResultsQueryPayload {
  search?: string;
  status?: string;
  remediationId?: string;
  deviceId?: string;
  model?: string;
  page?: number;
  pageSize?: number;
  sortKey?: string;
  sortDirection?: GridState["sortDirection"];
  columnFilters?: Record<string, string[]>;
  visibleColumns?: string[];
}

async function downloadFile(path: string, init: RequestInit, fallbackName: string) {
  const response = await fetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
  }

  const blob = await response.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  const disposition = response.headers.get("Content-Disposition");
  const match = disposition?.match(/filename=\"?([^\";]+)\"?/i);
  link.href = downloadUrl;
  link.download = match?.[1] ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(downloadUrl);
}

export const api = {
  getRemediations: () => requestJson<Remediation[]>("/remediations"),
  getResults: (payload: ResultsQueryPayload) =>
    requestJson<PagedResults>("/results/query", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  getResultFilterOptions: (columnKey: string, payload: ResultsQueryPayload) =>
    requestJson<string[]>("/results/filter-options", {
      method: "POST",
      body: JSON.stringify({
        columnKey,
        search: payload.search,
        status: payload.status,
        remediationId: payload.remediationId,
        deviceId: payload.deviceId,
        model: payload.model,
        columnFilters: payload.columnFilters,
      }),
    }),
  exportResults: (payload: ResultsQueryPayload) =>
    downloadFile(
      "/results/export",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(payload),
      },
      "in-remedy-results.csv",
    ),
  getSavedViews: () => requestJson<SavedView[]>("/saved-views"),
  resetData: () =>
    requestJson<void>("/admin/reset-data", {
      method: "POST",
    }),
  createSavedView: (payload: {
    ownerUserId: string;
    pageType: string;
    name: string;
    isDefault: boolean;
    viewDefinition: SavedViewDefinition;
  }) =>
    requestJson<SavedView>("/saved-views", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  setDefaultSavedView: (savedViewId: string) =>
    requestJson<void>(`/saved-views/${savedViewId}/set-default`, {
      method: "POST",
    }),
  deleteSavedView: (savedViewId: string) =>
    requestJson<void>(`/saved-views/${savedViewId}`, {
      method: "DELETE",
    }),
  getImports: () => requestJson<ImportBatch[]>("/imports"),
  getColumnLabels: () => requestJson<Record<string, string>>("/imports/column-labels"),
  getImport: (importBatchId: string) => requestJson<ImportBatch>(`/imports/${importBatchId}`),
  uploadResultsCsv: async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);

    const response = await fetch(`${apiBaseUrl}/imports/results-csv`, {
      method: "POST",
      body: formData,
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }

    const payload = await response.json();
    return (Array.isArray(payload) ? payload : [payload]) as ImportBatch[];
  },
  previewResultsCsv: async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);

    const response = await fetch(`${apiBaseUrl}/imports/preview-results-csv`, {
      method: "POST",
      body: formData,
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }

    return (await response.json()) as ImportPreview;
  },
  downloadTemplate: () => {
    window.open(`${apiBaseUrl}/imports/template`, "_blank", "noopener,noreferrer");
  },
};
