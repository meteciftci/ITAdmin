import { useCallback, useMemo, useState } from "react";

import {
  AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS,
  AD_ORGANIZATIONAL_UNITS_LIST_STORAGE_KEY,
  normalizeAdOrganizationalUnitsListState,
  parseAdOrganizationalUnitsListStateFromSession,
  type AdOrganizationalUnitsListState,
} from "@/features/ad-management/ad-ous-list-query";
import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "@/features/ad-management/ad-ous-list-path";

function canUseSessionStorage(): boolean {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

function readPersistedState(): AdOrganizationalUnitsListState {
  if (!canUseSessionStorage()) {
    return { ...AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS };
  }

  try {
    const raw = window.sessionStorage.getItem(AD_ORGANIZATIONAL_UNITS_LIST_STORAGE_KEY);
    return parseAdOrganizationalUnitsListStateFromSession(raw);
  } catch {
    return { ...AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS };
  }
}

function writePersistedState(state: AdOrganizationalUnitsListState): void {
  if (!canUseSessionStorage()) {
    return;
  }

  try {
    window.sessionStorage.setItem(AD_ORGANIZATIONAL_UNITS_LIST_STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore quota or privacy mode errors.
  }
}

export function useAdOrganizationalUnitListState() {
  const [listState, setListState] = useState<AdOrganizationalUnitsListState>(() => readPersistedState());

  const updateListState = useCallback((patch: Partial<AdOrganizationalUnitsListState>) => {
    setListState((current) => {
      const next = normalizeAdOrganizationalUnitsListState({ ...current, ...patch });
      writePersistedState(next);
      return next;
    });
  }, []);

  const clearListState = useCallback(() => {
    const defaults = { ...AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS };
    setListState(defaults);
    writePersistedState(defaults);
  }, []);

  const listPath = useMemo(() => AD_ORGANIZATIONAL_UNITS_LIST_PATH, []);

  return {
    listState,
    listPath,
    updateListState,
    clearListState,
  };
}
