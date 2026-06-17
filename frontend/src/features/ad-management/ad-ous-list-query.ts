export const AD_ORGANIZATIONAL_UNITS_LIST_STORAGE_KEY =
  "sasportal.adManagement.organizationalUnits.listState";

export type AdOrganizationalUnitsListState = {
  search: string;
  pageNumber: number;
  pageSize: number;
};

export const AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS: AdOrganizationalUnitsListState = {
  search: "",
  pageNumber: 1,
  pageSize: 25,
};

const PAGE_SIZE_OPTIONS = new Set([10, 25, 50]);

export function normalizeAdOrganizationalUnitsListState(
  state: Partial<AdOrganizationalUnitsListState> | null | undefined,
): AdOrganizationalUnitsListState {
  return {
    search:
      typeof state?.search === "string"
        ? state.search
        : AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.search,
    pageNumber: parsePositiveInt(state?.pageNumber, AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(state?.pageSize),
  };
}

export function parseAdOrganizationalUnitsListStateFromSession(
  raw: string | null,
): AdOrganizationalUnitsListState {
  if (!raw?.trim()) {
    return { ...AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS };
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AdOrganizationalUnitsListState> & {
      q?: string;
      page?: number;
    };

    return normalizeAdOrganizationalUnitsListState({
      search: typeof parsed.search === "string" ? parsed.search : parsed.q,
      pageNumber: parsed.pageNumber ?? parsed.page,
      pageSize: parsed.pageSize,
    });
  } catch {
    return { ...AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS };
  }
}

function parsePositiveInt(value: unknown, fallback: number): number {
  if (typeof value === "number") {
    return Number.isFinite(value) && value >= 1 ? Math.floor(value) : fallback;
  }

  if (typeof value !== "string" || !value.trim()) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return fallback;
  }

  return parsed;
}

function parsePageSize(value: unknown): number {
  const parsed = parsePositiveInt(value, AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.pageSize);
  return PAGE_SIZE_OPTIONS.has(parsed) ? parsed : AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.pageSize;
}
