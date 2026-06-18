import type { FormEvent } from "react";
import axios from "axios";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { ServiceUnavailableState } from "@/components/common/ServiceUnavailableState";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getAuthSessionOptions, getCurrentUser, login } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { AUTH_SESSION_OPTIONS_QUERY_KEY } from "@/features/auth/query-keys";
import type { LoginRouteReason } from "@/features/auth/self-role-change-relogin";
import { PublicLanguageSwitcher } from "@/features/auth/PublicLanguageSwitcher";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { i18n, normalizeLanguage } from "@/app/i18n";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { useReadinessStatus } from "@/hooks/useReadinessStatus";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

const SERVICE_UNAVAILABLE_ERROR_CODE = "ServiceUnavailable";
const LOGIN_ERROR_CODE = "LoginError";

const sanitizeForgotPasswordUrl = (value: string | null): string | null => {
  const trimmed = value?.trim();
  if (!trimmed) return null;
  return /^https?:\/\//i.test(trimmed) ? trimmed : null;
};

const shouldClearAuthAfterLoginFailure = (error: unknown): boolean => {
  if (error instanceof Error && error.message === SERVICE_UNAVAILABLE_ERROR_CODE) {
    return false;
  }

  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    if (status === 502 || status === 503 || status === 504) {
      return false;
    }
    if (!error.response) {
      return false;
    }
  }

  return true;
};

type LoginLocationState = {
  reason?: LoginRouteReason;
};

export function LoginPage() {
  const { t } = useTranslation(["auth", "common"]);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const setAuthenticated = useAuthStore((state) => state.setAuthenticated);
  const setUser = useAuthStore((state) => state.setUser);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  // Read the reason once during initial render so we can show a one-shot idle-timeout notice
  // without calling setState in an effect.
  const [routeNoticeMessage] = useState<string | null>(() => {
    const state = location.state as LoginLocationState | null;
    if (state?.reason === "idleTimeout") {
      return t("auth:sessionTimeout.expiredMessage");
    }
    if (state?.reason === "permissionsChanged") {
      return t("auth:permissionsChanged.reloginMessage");
    }
    return null;
  });

  useEffect(() => {
    const state = location.state as LoginLocationState | null;
    if (state?.reason === "idleTimeout" || state?.reason === "permissionsChanged") {
      navigate(location.pathname, { replace: true, state: null });
    }
    // We only care about cleaning up the router state once on mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      navigate("/home", { replace: true });
    }
  }, [isAuthenticated, navigate]);
  const readiness = useReadinessStatus();
  const sessionOptionsQuery = useQuery({
    queryKey: AUTH_SESSION_OPTIONS_QUERY_KEY,
    queryFn: getAuthSessionOptions,
    retry: false,
  });
  const rememberMeEnabled = sessionOptionsQuery.data?.rememberMeEnabled ?? true;
  const { data: branding } = useBrandingSettings();
  const appName = branding.applicationName || "ITAdmin";
  const resolvedLogoUrl = resolveApiAssetUrl(branding.logoUrl);
  const forgotPasswordUrl = sanitizeForgotPasswordUrl(branding.forgotPasswordUrl);
  const initials = appName
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  const resolveLoginErrorMessage = (error: unknown): string => {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      const data = error.response?.data as { errorCode?: string | null } | undefined;
      const errorCode = data?.errorCode ?? null;

      if (
        status === 502 ||
        status === 503 ||
        status === 504 ||
        errorCode === SERVICE_UNAVAILABLE_ERROR_CODE
      ) {
        return t("login.serviceUnavailable");
      }

      if (status === 500 || errorCode === LOGIN_ERROR_CODE) {
        return t("login.unexpectedServiceError");
      }

      if (status === 429) {
        return t("login.tooManyAttempts");
      }

      if (status === 401) {
        return t("login.error");
      }

      if (!error.response) {
        return t("login.networkError");
      }
    }

    return t("login.error");
  };

  const loginMutation = useMutation({
    mutationFn: async () => {
      const effectiveRememberMe = rememberMeEnabled ? rememberMe : false;
      const response = await login({
        userName,
        password,
        rememberMe: effectiveRememberMe,
      });
      if (!response.isSuccess) {
        if (response.errorCode === SERVICE_UNAVAILABLE_ERROR_CODE) {
          throw new Error(SERVICE_UNAVAILABLE_ERROR_CODE);
        }
        throw new Error(response.message || t("login.error"));
      }

      const currentUser = await getCurrentUser();
      setUser(currentUser);
      setAuthenticated(effectiveRememberMe);
      await i18n.changeLanguage(normalizeLanguage(currentUser.preferredLanguage));
    },
    onSuccess: () => {
      navigate("/home", { replace: true });
    },
    onError: (error: unknown) => {
      if (shouldClearAuthAfterLoginFailure(error)) {
        clearAuth();
      }

      if (error instanceof Error && error.message === SERVICE_UNAVAILABLE_ERROR_CODE) {
        setErrorMessage(t("login.serviceUnavailable"));
        return;
      }

      setErrorMessage(resolveLoginErrorMessage(error));
    },
  });

  const loginBlocked =
    readiness.isPending || Boolean(readiness.data && !readiness.isHealthy);

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);
    if (loginBlocked) {
      return;
    }
    loginMutation.mutate();
  };

  return (
    <main className="relative flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <div className="w-full max-w-md">
        <Card className="border-border/70 shadow-lg">
          <CardHeader className="space-y-2 text-center">
            <div className="mx-auto mb-2">
              {resolvedLogoUrl ? (
                <img src={resolvedLogoUrl} alt={appName} className="mx-auto h-14 w-14 rounded-md object-contain" />
              ) : (
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-md bg-muted text-lg font-semibold text-muted-foreground">
                  {initials || "SP"}
                </div>
              )}
            </div>
            <p className="text-sm font-medium text-muted-foreground">{appName}</p>
            <CardTitle className="text-2xl">{t("login.title")}</CardTitle>
            <CardDescription className="text-center">{t("login.description")}</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={onSubmit}>
              {routeNoticeMessage ? (
                <Alert>
                  <AlertDescription>{routeNoticeMessage}</AlertDescription>
                </Alert>
              ) : null}

              {readiness.isPending ? (
                <p className="text-xs text-muted-foreground">
                  {t("login.serviceCheckInProgress")}
                </p>
              ) : null}

              {readiness.data && !readiness.isHealthy ? (
                <ServiceUnavailableState
                  readiness={readiness.data}
                  compact
                  isLoading={readiness.isFetching}
                  onRetry={() => {
                    void queryClient.invalidateQueries({
                      queryKey: ["health", "readiness"],
                    });
                  }}
                />
              ) : null}

              <div className="space-y-2">
                <Label htmlFor="userName">{t("login.userName")}</Label>
                <Input
                  id="userName"
                  value={userName}
                  onChange={(event) => setUserName(event.target.value)}
                  autoComplete="username"
                  required
                  disabled={loginBlocked}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">{t("login.password")}</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="current-password"
                  required
                  disabled={loginBlocked}
                />
              </div>

              {rememberMeEnabled ? (
                <div className="flex items-start gap-3 rounded-md border border-border/60 bg-muted/20 p-3">
                  <Checkbox
                    id="rememberMe"
                    className="mt-0.5"
                    checked={rememberMe}
                    onChange={(event) => setRememberMe(event.target.checked)}
                    disabled={loginBlocked}
                  />
                  <div className="space-y-1">
                    <Label htmlFor="rememberMe" className="cursor-pointer font-normal leading-none">
                      {t("login.rememberMe")}
                    </Label>
                    <p className="text-xs text-muted-foreground">{t("login.rememberMeDescription")}</p>
                  </div>
                </div>
              ) : null}

              {forgotPasswordUrl ? (
                <div className="flex justify-end">
                  <a
                    href={forgotPasswordUrl}
                    className="text-sm text-primary hover:underline"
                  >
                    {t("login.forgotPassword")}
                  </a>
                </div>
              ) : null}

              {errorMessage ? (
                <Alert variant="destructive">
                  <AlertDescription>{errorMessage}</AlertDescription>
                </Alert>
              ) : null}

              <Button
                className="w-full"
                type="submit"
                disabled={loginMutation.isPending || loginBlocked}
              >
                {loginMutation.isPending ? t("login.loading") : t("login.submit")}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
      <div className="fixed bottom-6 right-6 flex items-center gap-2">
        <ThemeToggle />
        <PublicLanguageSwitcher />
      </div>
    </main>
  );
}
