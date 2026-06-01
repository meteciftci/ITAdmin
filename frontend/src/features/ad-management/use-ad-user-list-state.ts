import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";

import {
  AD_USERS_LIST_DEFAULTS,
  AD_USERS_LIST_STORAGE_KEY,
  normalizeAdUsersListState,
  parseAdUsersListStateFromSession,
  parseAdUsersListStateFromUrl,
  type AdUsersListState,
} from "@/features/ad-management/ad-users-list-query";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";

function canUseSessionStorage(): boolean {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

function readPersistedState(): AdUsersListState {
  if (!canUseSessionStorage()) {
    return { ...AD_USERS_LIST_DEFAULTS };
  }

  try {
    const raw = window.sessionStorage.getItem(AD_USERS_LIST_STORAGE_KEY);
    return parseAdUsersListStateFromSession(raw);
  } catch {
    return { ...AD_USERS_LIST_DEFAULTS };
  }
}

function writePersistedState(state: AdUsersListState): void {
  if (!canUseSessionStorage()) {
    return;
  }

  try {
    window.sessionStorage.setItem(AD_USERS_LIST_STORAGE_KEY, JSON.stringify(state));
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

export function useAdUserListState() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [listState, setListState] = useState<AdUsersListState>(() => readPersistedState());
  const migratedFromUrlRef = useRef(false);

  useEffect(() => {
    if (migratedFromUrlRef.current || !hasUrlListState(searchParams)) {
      return;
    }

    migratedFromUrlRef.current = true;
    const migrated = parseAdUsersListStateFromUrl(searchParams);
    setListState(migrated);
    writePersistedState(migrated);
    setSearchParams(new URLSearchParams(), { replace: true });
  }, [searchParams, setSearchParams]);

  const updateListState = useCallback((patch: Partial<AdUsersListState>) => {
    setListState((current) => {
      const next = normalizeAdUsersListState({ ...current, ...patch });
      writePersistedState(next);
      return next;
    });
  }, []);

  const clearListState = useCallback(() => {
    const defaults = { ...AD_USERS_LIST_DEFAULTS };
    setListState(defaults);
    writePersistedState(defaults);
  }, []);

  const listPath = useMemo(() => AD_USERS_LIST_PATH, []);

  return {
    listState,
    listPath,
    updateListState,
    clearListState,
  };
}
