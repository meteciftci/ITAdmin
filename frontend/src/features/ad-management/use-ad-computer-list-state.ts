import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";

import {
  AD_COMPUTERS_LIST_DEFAULTS,
  AD_COMPUTERS_LIST_STORAGE_KEY,
  normalizeAdComputersListState,
  parseAdComputersListStateFromSession,
  type AdComputersListState,
} from "@/features/ad-management/ad-computers-list-query";
import { AD_COMPUTERS_LIST_PATH } from "@/features/ad-management/ad-computers-list-path";

function canUseSessionStorage(): boolean {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

function readPersistedState(): AdComputersListState {
  if (!canUseSessionStorage()) {
    return { ...AD_COMPUTERS_LIST_DEFAULTS };
  }

  try {
    const raw = window.sessionStorage.getItem(AD_COMPUTERS_LIST_STORAGE_KEY);
    return parseAdComputersListStateFromSession(raw);
  } catch {
    return { ...AD_COMPUTERS_LIST_DEFAULTS };
  }
}

function writePersistedState(state: AdComputersListState): void {
  if (!canUseSessionStorage()) {
    return;
  }

  try {
    window.sessionStorage.setItem(AD_COMPUTERS_LIST_STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore quota or privacy mode errors; in-memory state still works.
  }
}

function hasUrlListState(searchParams: URLSearchParams): boolean {
  return (
    searchParams.has("q")
    || searchParams.has("status")
    || searchParams.has("page")
    || searchParams.has("pageSize")
  );
}

export function useAdComputerListState() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [listState, setListState] = useState<AdComputersListState>(() => readPersistedState());
  const migratedFromUrlRef = useRef(false);

  useEffect(() => {
    if (migratedFromUrlRef.current || !hasUrlListState(searchParams)) {
      return;
    }

    migratedFromUrlRef.current = true;
    const migrated = normalizeAdComputersListState({
      search: searchParams.get("q")?.trim() ?? AD_COMPUTERS_LIST_DEFAULTS.search,
      status: (searchParams.get("status") as AdComputersListState["status"] | null) ?? undefined,
      pageNumber: Number.parseInt(searchParams.get("page") ?? "", 10) || AD_COMPUTERS_LIST_DEFAULTS.pageNumber,
      pageSize: Number.parseInt(searchParams.get("pageSize") ?? "", 10) || AD_COMPUTERS_LIST_DEFAULTS.pageSize,
    });
    setListState(migrated);
    writePersistedState(migrated);
    setSearchParams(new URLSearchParams(), { replace: true });
  }, [searchParams, setSearchParams]);

  const updateListState = useCallback((patch: Partial<AdComputersListState>) => {
    setListState((current) => {
      const next = normalizeAdComputersListState({ ...current, ...patch });
      writePersistedState(next);
      return next;
    });
  }, []);

  const clearListState = useCallback(() => {
    const defaults = { ...AD_COMPUTERS_LIST_DEFAULTS };
    setListState(defaults);
    writePersistedState(defaults);
  }, []);

  const listPath = useMemo(() => AD_COMPUTERS_LIST_PATH, []);

  return {
    listState,
    listPath,
    updateListState,
    clearListState,
  };
}
