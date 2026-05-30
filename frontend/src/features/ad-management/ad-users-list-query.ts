import type { AdUserStatusFilter } from "@/features/ad-management/types";

export type AdUsersListQueryState = {
  q: string;
  status: AdUserStatusFilter;
  page: number;
  pageSize: number;
};

export const AD_USERS_LIST_DEFAULTS: AdUsersListQueryState = {
  q: "",
  status: "all",
  page: 1,
  pageSize: 20,
};

const VALID_STATUS: ReadonlySet<AdUserStatusFilter> = new Set([
  "active",
  "disabled",
  "all",
]);

const PAGE_SIZE_OPTIONS = new Set([10, 20, 25, 50]);

export function parseAdUsersListQuery(
  searchParams: URLSearchParams,
): AdUsersListQueryState {
  const rawStatus = searchParams.get("status");
  const status = VALID_STATUS.has(rawStatus as AdUserStatusFilter)
    ? (rawStatus as AdUserStatusFilter)
    : AD_USERS_LIST_DEFAULTS.status;

  const page = parsePositiveInt(searchParams.get("page"), AD_USERS_LIST_DEFAULTS.page);
  const pageSize = parsePageSize(searchParams.get("pageSize"));

  return {
    q: searchParams.get("q")?.trim() ?? AD_USERS_LIST_DEFAULTS.q,
    status,
    page,
    pageSize,
  };
}

export function buildAdUsersListSearchParams(
  state: AdUsersListQueryState,
): URLSearchParams {
  const params = new URLSearchParams();

  if (state.q.trim()) {
    params.set("q", state.q.trim());
  }

  if (state.status !== AD_USERS_LIST_DEFAULTS.status) {
    params.set("status", state.status);
  }

  if (state.page !== AD_USERS_LIST_DEFAULTS.page) {
    params.set("page", String(state.page));
  }

  if (state.pageSize !== AD_USERS_LIST_DEFAULTS.pageSize) {
    params.set("pageSize", String(state.pageSize));
  }

  return params;
}

export function buildAdUsersListPath(state: AdUsersListQueryState): string {
  const params = buildAdUsersListSearchParams(state);
  const query = params.toString();
  return query ? `/ad-management/users?${query}` : "/ad-management/users";
}

function parsePositiveInt(value: string | null, fallback: number): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return fallback;
  }

  return parsed;
}

function parsePageSize(value: string | null): number {
  const parsed = parsePositiveInt(value, AD_USERS_LIST_DEFAULTS.pageSize);
  return PAGE_SIZE_OPTIONS.has(parsed) ? parsed : AD_USERS_LIST_DEFAULTS.pageSize;
}
