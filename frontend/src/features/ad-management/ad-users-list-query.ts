import type { AdUserStatusFilter } from "@/features/ad-management/types";

export const AD_USERS_LIST_STORAGE_KEY = "itadmin.adManagement.users.listState";

export type AdUsersListState = {
  search: string;
  status: AdUserStatusFilter;
  pageNumber: number;
  pageSize: number;
};

/** @deprecated Use AdUsersListState. Kept for gradual migration in imports. */
export type AdUsersListQueryState = AdUsersListState;

export const AD_USERS_LIST_DEFAULTS: AdUsersListState = {
  search: "",
  status: "all",
  pageNumber: 1,
  pageSize: 20,
};

const VALID_STATUS: ReadonlySet<AdUserStatusFilter> = new Set([
  "active",
  "disabled",
  "all",
]);

const PAGE_SIZE_OPTIONS = new Set([10, 20, 25, 50]);

export function normalizeAdUsersListState(
  state: Partial<AdUsersListState> | null | undefined,
): AdUsersListState {
  const rawStatus = state?.status;
  const status = VALID_STATUS.has(rawStatus as AdUserStatusFilter)
    ? (rawStatus as AdUserStatusFilter)
    : AD_USERS_LIST_DEFAULTS.status;

  return {
    search: typeof state?.search === "string" ? state.search : AD_USERS_LIST_DEFAULTS.search,
    status,
    pageNumber: parsePositiveInt(state?.pageNumber, AD_USERS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(state?.pageSize),
  };
}

export function parseAdUsersListStateFromSession(raw: string | null): AdUsersListState {
  if (!raw?.trim()) {
    return { ...AD_USERS_LIST_DEFAULTS };
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AdUsersListState> & {
      q?: string;
      page?: number;
    };

    return normalizeAdUsersListState({
      search: typeof parsed.search === "string" ? parsed.search : parsed.q,
      status: parsed.status,
      pageNumber: parsed.pageNumber ?? parsed.page,
      pageSize: parsed.pageSize,
    });
  } catch {
    return { ...AD_USERS_LIST_DEFAULTS };
  }
}

export function parseAdUsersListStateFromUrl(
  searchParams: URLSearchParams,
): AdUsersListState {
  const rawStatus = searchParams.get("status");
  const status = VALID_STATUS.has(rawStatus as AdUserStatusFilter)
    ? (rawStatus as AdUserStatusFilter)
    : AD_USERS_LIST_DEFAULTS.status;

  return normalizeAdUsersListState({
    search: searchParams.get("q")?.trim() ?? AD_USERS_LIST_DEFAULTS.search,
    status,
    pageNumber: parsePositiveInt(searchParams.get("page"), AD_USERS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(searchParams.get("pageSize")),
  });
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
  const parsed = parsePositiveInt(value, AD_USERS_LIST_DEFAULTS.pageSize);
  return PAGE_SIZE_OPTIONS.has(parsed) ? parsed : AD_USERS_LIST_DEFAULTS.pageSize;
}
