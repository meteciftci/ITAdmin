import type { FormEvent } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getCurrentUser, login } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { PublicLanguageSwitcher } from "@/features/auth/PublicLanguageSwitcher";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { i18n, normalizeLanguage } from "@/app/i18n";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

const sanitizeForgotPasswordUrl = (value: string | null): string | null => {
  const trimmed = value?.trim();
  if (!trimmed) return null;
  return /^https?:\/\//i.test(trimmed) ? trimmed : null;
};

export function LoginPage() {
  const { t } = useTranslation(["auth"]);
  const navigate = useNavigate();
  const setTokens = useAuthStore((state) => state.setTokens);
  const setUser = useAuthStore((state) => state.setUser);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const { data: branding } = useBrandingSettings();
  const appName = branding.applicationName || "SAS Portal v2";
  const resolvedLogoUrl = resolveApiAssetUrl(branding.logoUrl);
  const forgotPasswordUrl = sanitizeForgotPasswordUrl(branding.forgotPasswordUrl);
  const initials = appName
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  const loginMutation = useMutation({
    mutationFn: async () => {
      const response = await login({ userName, password });
      if (!response.isSuccess) {
        throw new Error(response.message || t("login.error"));
      }

      setTokens({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        accessTokenExpiresAt: response.accessTokenExpiresAt,
        refreshTokenExpiresAt: response.refreshTokenExpiresAt,
      });

      const currentUser = await getCurrentUser();
      setUser(currentUser);
      await i18n.changeLanguage(normalizeLanguage(currentUser.preferredLanguage));
    },
    onSuccess: () => {
      navigate("/dashboard", { replace: true });
    },
    onError: () => {
      clearAuth();
      setErrorMessage(t("login.error"));
    },
  });

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);
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
              <div className="space-y-2">
                <Label htmlFor="userName">{t("login.userName")}</Label>
                <Input
                  id="userName"
                  value={userName}
                  onChange={(event) => setUserName(event.target.value)}
                  autoComplete="username"
                  required
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
                />
              </div>

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

              <Button className="w-full" type="submit" disabled={loginMutation.isPending}>
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
