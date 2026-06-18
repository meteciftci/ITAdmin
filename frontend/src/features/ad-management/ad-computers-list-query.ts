import type { AdComputerStatusFilter } from "@/features/ad-management/types";

export const AD_COMPUTERS_LIST_STORAGE_KEY = "itadmin.adManagement.computers.listState";

export type AdComputersListState = {
  search: string;
  status: AdComputerStatusFilter;
  operatingSystem: string;
  pageNumber: number;
  pageSize: number;
};

export const AD_COMPUTERS_LIST_DEFAULTS: AdComputersListState = {
  search: "",
  status: "active",
  operatingSystem: "",
  pageNumber: 1,
  pageSize: 20,
};

const VALID_STATUS: ReadonlySet<AdComputerStatusFilter> = new Set([
  "active",
  "disabled",
  "all",
]);

const PAGE_SIZE_OPTIONS = new Set([10, 20, 25, 50]);

export function normalizeAdComputersListState(
  state: Partial<AdComputersListState> | null | undefined,
): AdComputersListState {
  const rawStatus = state?.status;
  const status = VALID_STATUS.has(rawStatus as AdComputerStatusFilter)
    ? (rawStatus as AdComputerStatusFilter)
    : AD_COMPUTERS_LIST_DEFAULTS.status;

  return {
    search: typeof state?.search === "string" ? state.search : AD_COMPUTERS_LIST_DEFAULTS.search,
    status,
    operatingSystem:
      typeof state?.operatingSystem === "string"
        ? state.operatingSystem
        : AD_COMPUTERS_LIST_DEFAULTS.operatingSystem,
    pageNumber: parsePositiveInt(state?.pageNumber, AD_COMPUTERS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(state?.pageSize),
  };
}

export function parseAdComputersListStateFromSession(raw: string | null): AdComputersListState {
  if (!raw?.trim()) {
    return { ...AD_COMPUTERS_LIST_DEFAULTS };
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AdComputersListState> & {
      q?: string;
      page?: number;
    };

    return normalizeAdComputersListState({
      search: typeof parsed.search === "string" ? parsed.search : parsed.q,
      status: parsed.status,
      operatingSystem: parsed.operatingSystem,
      pageNumber: parsed.pageNumber ?? parsed.page,
      pageSize: parsed.pageSize,
    });
  } catch {
    return { ...AD_COMPUTERS_LIST_DEFAULTS };
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
  const parsed = parsePositiveInt(value, AD_COMPUTERS_LIST_DEFAULTS.pageSize);
  return PAGE_SIZE_OPTIONS.has(parsed) ? parsed : AD_COMPUTERS_LIST_DEFAULTS.pageSize;
}
