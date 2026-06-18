export const AD_GROUPS_LIST_STORAGE_KEY = "itadmin.adManagement.groups.listState";

export type AdGroupsListState = {
  search: string;
  pageNumber: number;
  pageSize: number;
};

export const AD_GROUPS_LIST_DEFAULTS: AdGroupsListState = {
  search: "",
  pageNumber: 1,
  pageSize: 20,
};

const PAGE_SIZE_OPTIONS = new Set([10, 20, 25, 50]);

export function normalizeAdGroupsListState(
  state: Partial<AdGroupsListState> | null | undefined,
): AdGroupsListState {
  return {
    search: typeof state?.search === "string" ? state.search : AD_GROUPS_LIST_DEFAULTS.search,
    pageNumber: parsePositiveInt(state?.pageNumber, AD_GROUPS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(state?.pageSize),
  };
}

export function parseAdGroupsListStateFromSession(raw: string | null): AdGroupsListState {
  if (!raw?.trim()) {
    return { ...AD_GROUPS_LIST_DEFAULTS };
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AdGroupsListState> & {
      q?: string;
      page?: number;
    };

    return normalizeAdGroupsListState({
      search: typeof parsed.search === "string" ? parsed.search : parsed.q,
      pageNumber: parsed.pageNumber ?? parsed.page,
      pageSize: parsed.pageSize,
    });
  } catch {
    return { ...AD_GROUPS_LIST_DEFAULTS };
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
  const parsed = parsePositiveInt(value, AD_GROUPS_LIST_DEFAULTS.pageSize);
  return PAGE_SIZE_OPTIONS.has(parsed) ? parsed : AD_GROUPS_LIST_DEFAULTS.pageSize;
}
