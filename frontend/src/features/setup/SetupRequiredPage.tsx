import axios from "axios";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useNavigate } from "react-router-dom";
import { toast } from "sonner";

import { PublicLanguageSwitcher } from "@/features/auth/PublicLanguageSwitcher";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SectionCard } from "@/components/common/SectionCard";
import { completeSetup, getSetupStatus } from "@/features/setup/api";
import {
  buildCompleteSetupRequest,
  createDefaultSetupFormValues,
  mapCompleteSetupFailureToast,
  resolveResponseMessage,
  type SetupFormValues,
} from "@/features/setup/setup-form";
import { getApiErrorMessage } from "@/lib/api-error";

const SETUP_STATUS_QUERY_KEY = ["setup", "status"] as const;

type FieldErrors = Partial<Record<string, string>>;

function FieldHint({ children }: { children: string }) {
  return <p className="text-xs text-muted-foreground">{children}</p>;
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs text-destructive">{message}</p>;
}

export function SetupRequiredPage() {
  const { t } = useTranslation(["setup", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [values, setValues] = useState<SetupFormValues>(() =>
    createDefaultSetupFormValues(t("setup:defaults.connectionName")),
  );
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const setupQuery = useQuery({
    queryKey: SETUP_STATUS_QUERY_KEY,
    queryFn: getSetupStatus,
  });

  const validateForm = (): FieldErrors => {
    const errors: FieldErrors = {};

    if (!values.setupKey.trim()) {
      errors.setupKey = t("setup:validation.required");
    }

    if (!values.ldap.host.trim()) {
      errors["ldap.host"] = t("setup:validation.required");
    }

    if (!values.ldap.baseDn.trim()) {
      errors["ldap.baseDn"] = t("setup:validation.required");
    }

    if (!values.ldap.userSearchBase.trim()) {
      errors["ldap.userSearchBase"] = t("setup:validation.required");
    }

    if (!values.ldap.userSearchFilter.trim()) {
      errors["ldap.userSearchFilter"] = t("setup:validation.required");
    }

    if (!values.ldap.bindUserName.trim()) {
      errors["ldap.bindUserName"] = t("setup:validation.required");
    }

    if (!values.ldap.bindPassword.trim()) {
      errors["ldap.bindPassword"] = t("setup:validation.required");
    }

    if (!values.admin.userName.trim()) {
      errors["admin.userName"] = t("setup:validation.required");
    }

    return errors;
  };

  const handleCompleteSetup = async () => {
    const errors = validateForm();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setIsSubmitting(true);
    const completeFailedFallback = t("setup:messages.completeFailed");
    const failureHints = {
      genericFallback: completeFailedFallback,
      directoryUserNotFoundHint: t("setup:messages.directoryUserNotFoundHint"),
      directoryUserProfileHint: t("setup:messages.directoryUserProfileHint"),
      ldapTimeoutHint: t("setup:messages.ldapTimeoutHint"),
    };
    try {
      const response = await completeSetup(buildCompleteSetupRequest(values));
      if (!response.isCompleted) {
        toast.error(mapCompleteSetupFailureToast(response.message, failureHints));
        return;
      }

      await queryClient.invalidateQueries({ queryKey: SETUP_STATUS_QUERY_KEY });
      toast.success(
        resolveResponseMessage(response.message, t("setup:messages.completeSuccess")),
      );
      navigate("/login", { replace: true });
    } catch (error) {
      if (axios.isAxiosError(error)) {
        const data = error.response?.data as { message?: string; isCompleted?: boolean } | undefined;
        if (data && typeof data === "object" && data.isCompleted === false) {
          toast.error(mapCompleteSetupFailureToast(data.message, failureHints));
          return;
        }
      }

      const fromApi = axios.isAxiosError(error)
        ? getApiErrorMessage(error, completeFailedFallback)
        : completeFailedFallback;

      toast.error(mapCompleteSetupFailureToast(fromApi, failureHints));
    } finally {
      setIsSubmitting(false);
    }
  };

  function updateLdap<K extends keyof SetupFormValues["ldap"]>(
    field: K,
    value: SetupFormValues["ldap"][K],
  ) {
    setValues((current) => ({
      ...current,
      ldap: { ...current.ldap, [field]: value },
    }));
  }

  function updateAdmin<K extends keyof SetupFormValues["admin"]>(
    field: K,
    value: SetupFormValues["admin"][K],
  ) {
    setValues((current) => ({
      ...current,
      admin: { ...current.admin, [field]: value },
    }));
  }

  if (setupQuery.isLoading) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
      </main>
    );
  }

  if (setupQuery.data && !setupQuery.data.isSetupRequired) {
    return <Navigate to="/login" replace />;
  }

  return (
    <main className="relative min-h-screen bg-muted/30 p-4 md:p-8">
      <div className="mx-auto max-w-3xl space-y-6">
        <Card className="border-border/70 shadow-lg">
          <CardHeader className="space-y-2">
            <CardTitle className="text-2xl">{t("setup:title")}</CardTitle>
            <CardDescription>{t("setup:description")}</CardDescription>
          </CardHeader>
        </Card>

        <form
          className="space-y-6"
          onSubmit={(event) => {
            event.preventDefault();
            void handleCompleteSetup();
          }}
        >
          <SectionCard title={t("setup:sections.setupKey")}>
            <div className="space-y-2">
              <Label htmlFor="setupKey">{t("setup:fields.setupKey")}</Label>
              <Input
                id="setupKey"
                type="password"
                autoComplete="off"
                value={values.setupKey}
                onChange={(event) => {
                  setValues((current) => ({ ...current, setupKey: event.target.value }));
                  setFieldErrors((current) => {
                    const next = { ...current };
                    delete next.setupKey;
                    return next;
                  });
                }}
                disabled={isSubmitting}
                required
              />
              <FieldError message={fieldErrors.setupKey} />
            </div>
          </SectionCard>

          <SectionCard title={t("setup:sections.ldap")}>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapName">{t("setup:fields.name")}</Label>
                <Input
                  id="ldapName"
                  value={values.ldap.name}
                  onChange={(event) => updateLdap("name", event.target.value)}
                  disabled={isSubmitting}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="ldapHost">{t("setup:fields.host")}</Label>
                <Input
                  id="ldapHost"
                  value={values.ldap.host}
                  onChange={(event) => updateLdap("host", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldHint>{t("setup:helpers.host")}</FieldHint>
                <FieldError message={fieldErrors["ldap.host"]} />
              </div>

              <div className="space-y-2 md:col-span-2">
                <p className="text-xs text-muted-foreground">{t("setup:helpers.ldapsHelp")}</p>
              </div>

              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapBaseDn">{t("setup:fields.baseDn")}</Label>
                <Input
                  id="ldapBaseDn"
                  value={values.ldap.baseDn}
                  onChange={(event) => updateLdap("baseDn", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldError message={fieldErrors["ldap.baseDn"]} />
              </div>

              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapUserSearchBase">{t("setup:fields.userSearchBase")}</Label>
                <Input
                  id="ldapUserSearchBase"
                  value={values.ldap.userSearchBase}
                  onChange={(event) => updateLdap("userSearchBase", event.target.value)}
                  disabled={isSubmitting}
                />
                <FieldError message={fieldErrors["ldap.userSearchBase"]} />
              </div>

              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapUserSearchFilter">{t("setup:fields.userSearchFilter")}</Label>
                <Input
                  id="ldapUserSearchFilter"
                  value={values.ldap.userSearchFilter}
                  onChange={(event) => updateLdap("userSearchFilter", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldHint>{t("setup:helpers.userSearchFilter")}</FieldHint>
                <FieldError message={fieldErrors["ldap.userSearchFilter"]} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="ldapBindUserName">{t("setup:fields.bindUserName")}</Label>
                <Input
                  id="ldapBindUserName"
                  value={values.ldap.bindUserName}
                  onChange={(event) => updateLdap("bindUserName", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldHint>{t("setup:helpers.bindUserName")}</FieldHint>
                <FieldError message={fieldErrors["ldap.bindUserName"]} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="ldapBindUserDomain">{t("setup:fields.bindUserDomain")}</Label>
                <Input
                  id="ldapBindUserDomain"
                  value={values.ldap.bindUserDomain}
                  onChange={(event) => updateLdap("bindUserDomain", event.target.value)}
                  disabled={isSubmitting}
                />
                <FieldHint>{t("setup:helpers.bindUserDomain")}</FieldHint>
              </div>

              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapBindPassword">{t("setup:fields.bindPassword")}</Label>
                <Input
                  id="ldapBindPassword"
                  type="password"
                  autoComplete="off"
                  value={values.ldap.bindPassword}
                  onChange={(event) => updateLdap("bindPassword", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldError message={fieldErrors["ldap.bindPassword"]} />
              </div>
            </div>
          </SectionCard>

          <SectionCard title={t("setup:sections.admin")}>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2 sm:col-span-2">
                <Label htmlFor="adminUserName">{t("setup:fields.adminUserName")}</Label>
                <Input
                  id="adminUserName"
                  autoComplete="username"
                  value={values.admin.userName}
                  onChange={(event) => updateAdmin("userName", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldError message={fieldErrors["admin.userName"]} />
              </div>
            </div>
          </SectionCard>

          <div className="flex justify-end">
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t("setup:actions.submitting") : t("setup:actions.complete")}
            </Button>
          </div>
        </form>
      </div>

      <div className="fixed bottom-6 right-6 flex items-center gap-2">
        <ThemeToggle />
        <PublicLanguageSwitcher />
      </div>
    </main>
  );
}
