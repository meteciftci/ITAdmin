import type { DateRange } from "react-day-picker";
import { format, parseISO } from "date-fns";

import {
  LICENSE_REQUESTS_LIST_PATH,
} from "@/features/license-management/license-request-paths";
import type { LicenseRequestSource, LicenseRequestStatus } from "@/features/license-management/types";
import { REQUEST_SOURCES, REQUEST_STATUSES } from "@/features/license-management/enum-labels";

export type LicenseRequestsListState = {
  search: string;
  status: "all" | LicenseRequestStatus;
  requestSource: "all" | LicenseRequestSource;
  productId: "all" | string;
  dateRange: DateRange | undefined;
  pageNumber: number;
  pageSize: number;
};

export const LICENSE_REQUESTS_LIST_DEFAULTS: LicenseRequestsListState = {
  search: "",
  status: "all",
  requestSource: "all",
  productId: "all",
  dateRange: undefined,
  pageNumber: 1,
  pageSize: 20,
};

const VALID_STATUSES = new Set<LicenseRequestStatus>(REQUEST_STATUSES);

const VALID_SOURCES = new Set<LicenseRequestSource>(REQUEST_SOURCES);

const VALID_PAGE_SIZES = new Set([10, 20, 25, 50]);

function parsePositiveInt(value: string | null, fallback: number): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function parsePageSize(value: string | null): number {
  const parsed = parsePositiveInt(value, LICENSE_REQUESTS_LIST_DEFAULTS.pageSize);
  return VALID_PAGE_SIZES.has(parsed) ? parsed : LICENSE_REQUESTS_LIST_DEFAULTS.pageSize;
}

function parseDateRange(
  fromValue: string | null,
  toValue: string | null,
): DateRange | undefined {
  if (!fromValue && !toValue) {
    return undefined;
  }

  const from = fromValue ? parseISO(fromValue) : undefined;
  const to = toValue ? parseISO(toValue) : undefined;

  if (from && Number.isNaN(from.getTime())) {
    return undefined;
  }

  if (to && Number.isNaN(to.getTime())) {
    return undefined;
  }

  return { from, to };
}

export function parseLicenseRequestsListStateFromUrl(
  searchParams: URLSearchParams,
): LicenseRequestsListState {
  const rawStatus = searchParams.get("status");
  const status = rawStatus && VALID_STATUSES.has(rawStatus as LicenseRequestStatus)
    ? (rawStatus as LicenseRequestStatus)
    : LICENSE_REQUESTS_LIST_DEFAULTS.status;

  const rawSource = searchParams.get("requestSource");
  const requestSource = rawSource && VALID_SOURCES.has(rawSource as LicenseRequestSource)
    ? (rawSource as LicenseRequestSource)
    : LICENSE_REQUESTS_LIST_DEFAULTS.requestSource;

  const productId = searchParams.get("productId")?.trim() || LICENSE_REQUESTS_LIST_DEFAULTS.productId;

  return {
    search: searchParams.get("search")?.trim() ?? LICENSE_REQUESTS_LIST_DEFAULTS.search,
    status,
    requestSource,
    productId,
    dateRange: parseDateRange(
      searchParams.get("requestDateFrom"),
      searchParams.get("requestDateTo"),
    ),
    pageNumber: parsePositiveInt(searchParams.get("pageNumber"), LICENSE_REQUESTS_LIST_DEFAULTS.pageNumber),
    pageSize: parsePageSize(searchParams.get("pageSize")),
  };
}

export function buildLicenseRequestsListPath(state: LicenseRequestsListState): string {
  const params = new URLSearchParams();

  if (state.search.trim()) {
    params.set("search", state.search.trim());
  }

  if (state.status !== LICENSE_REQUESTS_LIST_DEFAULTS.status) {
    params.set("status", state.status);
  }

  if (state.requestSource !== LICENSE_REQUESTS_LIST_DEFAULTS.requestSource) {
    params.set("requestSource", state.requestSource);
  }

  if (state.productId !== LICENSE_REQUESTS_LIST_DEFAULTS.productId) {
    params.set("productId", state.productId);
  }

  if (state.dateRange?.from) {
    params.set("requestDateFrom", format(state.dateRange.from, "yyyy-MM-dd"));
  }

  if (state.dateRange?.to) {
    params.set("requestDateTo", format(state.dateRange.to, "yyyy-MM-dd"));
  }

  if (state.pageNumber !== LICENSE_REQUESTS_LIST_DEFAULTS.pageNumber) {
    params.set("pageNumber", String(state.pageNumber));
  }

  if (state.pageSize !== LICENSE_REQUESTS_LIST_DEFAULTS.pageSize) {
    params.set("pageSize", String(state.pageSize));
  }

  const query = params.toString();
  return query ? `${LICENSE_REQUESTS_LIST_PATH}?${query}` : LICENSE_REQUESTS_LIST_PATH;
}

export function hasLicenseRequestsListUrlState(searchParams: URLSearchParams): boolean {
  return [
    "search",
    "status",
    "requestSource",
    "productId",
    "requestDateFrom",
    "requestDateTo",
    "pageNumber",
    "pageSize",
  ].some((key) => searchParams.has(key));
}
