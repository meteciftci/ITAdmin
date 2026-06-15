import { useCallback, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";

import {
  AD_DELETED_OBJECTS_LIST_DEFAULTS,
  AD_DELETED_OBJECTS_LIST_PATH,
  AD_DELETED_OBJECTS_LIST_STORAGE_KEY,
  normalizeAdDeletedObjectsListState,
  parseAdDeletedObjectsListStateFromSession,
  type AdDeletedObjectsListState,
} from "@/features/ad-management/ad-deleted-objects-list-query";

function canUseSessionStorage(): boolean {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

function readPersistedState(): AdDeletedObjectsListState {
  if (!canUseSessionStorage()) {
    return { ...AD_DELETED_OBJECTS_LIST_DEFAULTS };
  }

  try {
    const raw = window.sessionStorage.getItem(AD_DELETED_OBJECTS_LIST_STORAGE_KEY);
    return parseAdDeletedObjectsListStateFromSession(raw);
  } catch {
    return { ...AD_DELETED_OBJECTS_LIST_DEFAULTS };
  }
}

function writePersistedState(state: AdDeletedObjectsListState): void {
  if (!canUseSessionStorage()) {
    return;
  }

  try {
    window.sessionStorage.setItem(AD_DELETED_OBJECTS_LIST_STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore quota or privacy mode errors.
  }
}

export function useAdDeletedObjectListState() {
  const [searchParams] = useSearchParams();
  const [listState, setListState] = useState<AdDeletedObjectsListState>(() => readPersistedState());

  const listPath = useMemo(() => {
    const params = new URLSearchParams();
    if (listState.search.trim()) {
      params.set("q", listState.search.trim());
    }
    if (listState.type !== AD_DELETED_OBJECTS_LIST_DEFAULTS.type) {
      params.set("type", listState.type);
    }
    if (listState.pageNumber !== AD_DELETED_OBJECTS_LIST_DEFAULTS.pageNumber) {
      params.set("page", String(listState.pageNumber));
    }
    if (listState.pageSize !== AD_DELETED_OBJECTS_LIST_DEFAULTS.pageSize) {
      params.set("pageSize", String(listState.pageSize));
    }

    const query = params.toString();
    return query ? `${AD_DELETED_OBJECTS_LIST_PATH}?${query}` : AD_DELETED_OBJECTS_LIST_PATH;
  }, [listState]);

  const updateListState = useCallback((patch: Partial<AdDeletedObjectsListState>) => {
    setListState((current) => {
      const next = normalizeAdDeletedObjectsListState({ ...current, ...patch });
      writePersistedState(next);
      return next;
    });
  }, []);

  const clearListState = useCallback(() => {
    setListState({ ...AD_DELETED_OBJECTS_LIST_DEFAULTS });
    writePersistedState({ ...AD_DELETED_OBJECTS_LIST_DEFAULTS });
  }, []);

  return {
    listState,
    listPath,
    searchParams,
    updateListState,
    clearListState,
  };
}
