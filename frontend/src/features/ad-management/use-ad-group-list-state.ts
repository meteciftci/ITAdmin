import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";

import {
  AD_GROUPS_LIST_DEFAULTS,
  AD_GROUPS_LIST_STORAGE_KEY,
  normalizeAdGroupsListState,
  parseAdGroupsListStateFromSession,
  type AdGroupsListState,
} from "@/features/ad-management/ad-groups-list-query";
import { AD_GROUPS_LIST_PATH } from "@/features/ad-management/ad-groups-list-path";

function canUseSessionStorage(): boolean {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

function readPersistedState(): AdGroupsListState {
  if (!canUseSessionStorage()) {
    return { ...AD_GROUPS_LIST_DEFAULTS };
  }

  try {
    const raw = window.sessionStorage.getItem(AD_GROUPS_LIST_STORAGE_KEY);
    return parseAdGroupsListStateFromSession(raw);
  } catch {
    return { ...AD_GROUPS_LIST_DEFAULTS };
  }
}

function writePersistedState(state: AdGroupsListState): void {
  if (!canUseSessionStorage()) {
    return;
  }

  try {
    window.sessionStorage.setItem(AD_GROUPS_LIST_STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore quota or privacy mode errors; in-memory state still works.
  }
}

function hasUrlListState(searchParams: URLSearchParams): boolean {
  return (
    searchParams.has("q")
    || searchParams.has("page")
    || searchParams.has("pageSize")
  );
}

export function useAdGroupListState() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [listState, setListState] = useState<AdGroupsListState>(() => readPersistedState());
  const migratedFromUrlRef = useRef(false);

  useEffect(() => {
    if (migratedFromUrlRef.current || !hasUrlListState(searchParams)) {
      return;
    }

    migratedFromUrlRef.current = true;
    const migrated = normalizeAdGroupsListState({
      search: searchParams.get("q")?.trim() ?? AD_GROUPS_LIST_DEFAULTS.search,
      pageNumber: Number.parseInt(searchParams.get("page") ?? "", 10) || AD_GROUPS_LIST_DEFAULTS.pageNumber,
      pageSize: Number.parseInt(searchParams.get("pageSize") ?? "", 10) || AD_GROUPS_LIST_DEFAULTS.pageSize,
    });
    setListState(migrated);
    writePersistedState(migrated);
    setSearchParams(new URLSearchParams(), { replace: true });
  }, [searchParams, setSearchParams]);

  const updateListState = useCallback((patch: Partial<AdGroupsListState>) => {
    setListState((current) => {
      const next = normalizeAdGroupsListState({ ...current, ...patch });
      writePersistedState(next);
      return next;
    });
  }, []);

  const clearListState = useCallback(() => {
    const defaults = { ...AD_GROUPS_LIST_DEFAULTS };
    setListState(defaults);
    writePersistedState(defaults);
  }, []);

  const listPath = useMemo(() => AD_GROUPS_LIST_PATH, []);

  return {
    listState,
    listPath,
    updateListState,
    clearListState,
  };
}
