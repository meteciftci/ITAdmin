import { useCallback, useEffect, useRef, useState } from "react";
import axios from "axios";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import {
  AUTH_SESSION_OPTIONS_DEFAULTS,
  getAuthSessionOptions,
  logout as logoutApi,
  refreshToken as refreshTokenApi,
} from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { IdleTimeoutDialog } from "@/features/auth/components/IdleTimeoutDialog";

const ACTIVITY_THROTTLE_MS = 15_000;
const TICK_INTERVAL_MS = 1_000;

const ACTIVITY_EVENTS: Array<keyof WindowEventMap> = [
  "pointerdown",
  "keydown",
  "scroll",
  "touchstart",
  "mousemove",
];

type RefreshOutcome =
  | { kind: "success" }
  | { kind: "expired" }
  | { kind: "transient" };

const attemptIdleRefresh = async (
  setTokens: ReturnType<typeof useAuthStore.getState>["setTokens"],
  rememberMe: boolean,
): Promise<RefreshOutcome> => {
  try {
    const response = await refreshTokenApi();

    if (response.isSuccess && response.accessToken) {
      setTokens(
        {
          accessToken: response.accessToken,
          accessTokenExpiresAt: response.accessTokenExpiresAt,
        },
        rememberMe,
      );
      return { kind: "success" };
    }

    return { kind: "expired" };
  } catch (error) {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      if (!error.response || status === 502 || status === 503 || status === 504) {
        return { kind: "transient" };
      }
      return { kind: "expired" };
    }

    return { kind: "transient" };
  }
};

export function IdleSessionManager() {
  const { t } = useTranslation(["auth"]);
  const navigate = useNavigate();

  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const accessToken = useAuthStore((state) => state.accessToken);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const setTokens = useAuthStore((state) => state.setTokens);

  const sessionOptionsQuery = useQuery({
    queryKey: ["auth", "session-options"],
    queryFn: getAuthSessionOptions,
    enabled: Boolean(accessToken),
    staleTime: 5 * 60 * 1000,
    retry: 1,
    refetchOnWindowFocus: true,
  });

  const options = sessionOptionsQuery.data ?? AUTH_SESSION_OPTIONS_DEFAULTS;
  const idleTimeoutMs = options.idleTimeoutMinutes * 60 * 1000;
  const warningMs = options.idleWarningSeconds * 1000;

  const lastActivityAtRef = useRef<number>(0);
  const lastActivityWriteRef = useRef<number>(0);
  const isExtendingRef = useRef(false);
  const isExpiredRef = useRef(false);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [remainingSeconds, setRemainingSeconds] = useState(0);
  const [isExtending, setIsExtending] = useState(false);

  const recordActivity = useCallback(() => {
    if (dialogOpen) {
      return;
    }
    const now = Date.now();
    if (now - lastActivityWriteRef.current < ACTIVITY_THROTTLE_MS) {
      return;
    }
    lastActivityWriteRef.current = now;
    lastActivityAtRef.current = now;
  }, [dialogOpen]);

  const expireSession = useCallback(async () => {
    if (isExpiredRef.current) {
      return;
    }
    isExpiredRef.current = true;

    try {
      await logoutApi();
    } catch {
      // best-effort logout; backend may be unreachable
    }

    clearAuth();
    setDialogOpen(false);
    setIsExtending(false);
    navigate("/login", { replace: true, state: { reason: "idleTimeout" } });
  }, [clearAuth, navigate]);

  const handleSignOut = useCallback(async () => {
    if (isExpiredRef.current) {
      return;
    }
    isExpiredRef.current = true;

    try {
      await logoutApi();
    } catch {
      // best-effort logout
    }

    clearAuth();
    setDialogOpen(false);
    setIsExtending(false);
    navigate("/login", { replace: true });
  }, [clearAuth, navigate]);

  const handleContinue = useCallback(async () => {
    if (isExtendingRef.current || isExpiredRef.current) {
      return;
    }

    const currentRememberMe = useAuthStore.getState().rememberMe;

    isExtendingRef.current = true;
    setIsExtending(true);

    const outcome = await attemptIdleRefresh(setTokens, currentRememberMe);

    isExtendingRef.current = false;
    setIsExtending(false);

    if (outcome.kind === "success") {
      const now = Date.now();
      lastActivityAtRef.current = now;
      lastActivityWriteRef.current = now;
      setDialogOpen(false);
      return;
    }

    if (outcome.kind === "transient") {
      toast.error(t("auth:sessionTimeout.extendFailed"));
      return;
    }

    await expireSession();
  }, [expireSession, setTokens, t]);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    // The auth-store is our external system: each new access token represents a fresh session
    // (initial login or refresh). Resetting refs (not React state) syncs the idle bookkeeping
    // with that source of truth without triggering extra renders.
    const now = Date.now();
    lastActivityAtRef.current = now;
    lastActivityWriteRef.current = now;
    isExpiredRef.current = false;
  }, [isAuthenticated, accessToken]);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    // Session policy can be changed from Settings while the user is signed in.
    // Treat the refetched policy as fresh activity so the active session adopts the
    // new timeout values without requiring a browser refresh or re-login.
    const now = Date.now();
    lastActivityAtRef.current = now;
    lastActivityWriteRef.current = now;
  }, [isAuthenticated, idleTimeoutMs, warningMs]);

  useEffect(() => {
    if (!isAuthenticated || dialogOpen) {
      return;
    }

    const handler = () => recordActivity();
    for (const eventName of ACTIVITY_EVENTS) {
      window.addEventListener(eventName, handler, { passive: true });
    }
    window.addEventListener("focus", handler);
    document.addEventListener("visibilitychange", handler);

    return () => {
      for (const eventName of ACTIVITY_EVENTS) {
        window.removeEventListener(eventName, handler);
      }
      window.removeEventListener("focus", handler);
      document.removeEventListener("visibilitychange", handler);
    };
  }, [isAuthenticated, dialogOpen, recordActivity]);

  useEffect(() => {
    if (!isAuthenticated || idleTimeoutMs <= 0) {
      return;
    }

    const tick = () => {
      if (isExpiredRef.current) {
        return;
      }
      const now = Date.now();
      const expiresAt = lastActivityAtRef.current + idleTimeoutMs;
      const warningAt = expiresAt - warningMs;

      if (now >= expiresAt) {
        void expireSession();
        return;
      }

      if (now >= warningAt) {
        const remaining = Math.max(0, Math.ceil((expiresAt - now) / 1000));
        setRemainingSeconds(remaining);
        if (!dialogOpen) {
          setDialogOpen(true);
        }
        return;
      }

      if (dialogOpen) {
        setDialogOpen(false);
      }
    };

    tick();
    const intervalId = window.setInterval(tick, TICK_INTERVAL_MS);
    return () => {
      window.clearInterval(intervalId);
    };
  }, [isAuthenticated, idleTimeoutMs, warningMs, dialogOpen, expireSession]);

  if (!isAuthenticated) {
    return null;
  }

  return (
    <IdleTimeoutDialog
      open={dialogOpen}
      remainingSeconds={remainingSeconds}
      isExtending={isExtending}
      onContinue={() => {
        void handleContinue();
      }}
      onSignOut={() => {
        void handleSignOut();
      }}
    />
  );
}
