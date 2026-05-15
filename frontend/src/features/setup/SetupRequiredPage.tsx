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
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SectionCard } from "@/components/common/SectionCard";
import {
  completeSetup,
  getSetupStatus,
  type CompleteSetupRequest,
} from "@/features/setup/api";
import { getApiErrorMessage } from "@/lib/api-error";

const SETUP_STATUS_QUERY_KEY = ["setup", "status"] as const;

const DIRECTORY_USER_NOT_FOUND = "Directory user could not be found.";
const DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX = "Directory user profile could not be loaded";
const LDAP_OPERATION_TIMED_OUT_PREFIX = "LDAP operation timed out";

const STANDARD_LDAP_PORT = "389";
const STANDARD_LDAPS_PORT = "636";

function shouldSuggestPortForSslToggle(portValue: string): boolean {
  const trimmed = portValue.trim();
  if (trimmed.length === 0) {
    return true;
  }
  return trimmed === STANDARD_LDAP_PORT || trimmed === STANDARD_LDAPS_PORT;
}

function portAfterSslToggle(useSsl: boolean, currentPort: string): string {
  if (!shouldSuggestPortForSslToggle(currentPort)) {
    return currentPort;
  }
  return useSsl ? STANDARD_LDAPS_PORT : STANDARD_LDAP_PORT;
}

type SetupFormValues = {
  setupKey: string;
  ldap: {
    name: string;
    host: string;
    port: string;
    useSsl: boolean;
    baseDn: string;
    userSearchBase: string;
    userSearchFilter: string;
    bindUserName: string;
    bindUserDomain: string;
    bindPassword: string;
    nationalIdAttribute: string;
  };
  admin: {
    userName: string;
    password: string;
  };
};

type FieldErrors = Partial<Record<string, string>>;

const defaultValues: SetupFormValues = {
  setupKey: "",
  ldap: {
    name: "Default LDAP",
    host: "",
    port: STANDARD_LDAP_PORT,
    useSsl: false,
    baseDn: "",
    userSearchBase: "",
    userSearchFilter: "(&(objectClass=user)(sAMAccountName={0}))",
    bindUserName: "",
    bindUserDomain: "",
    bindPassword: "",
    nationalIdAttribute: "",
  },
  admin: {
    userName: "",
    password: "",
  },
};

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function resolveResponseMessage(message: string | undefined, fallback: string): string {
  const trimmed = message?.trim();
  return trimmed ? trimmed : fallback;
}

function mapCompleteSetupFailureToast(
  backendMessage: string | undefined,
  genericFallback: string,
  directoryUserNotFoundHint: string,
  directoryUserProfileHint: string,
  ldapTimeoutHint: string,
): string {
  const trimmed = backendMessage?.trim() ?? "";
  if (trimmed === DIRECTORY_USER_NOT_FOUND) {
    return directoryUserNotFoundHint;
  }
  if (trimmed.startsWith(DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX)) {
    return directoryUserProfileHint;
  }
  if (trimmed.startsWith(LDAP_OPERATION_TIMED_OUT_PREFIX)) {
    return ldapTimeoutHint;
  }
  return trimmed.length > 0 ? trimmed : genericFallback;
}

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

  const [values, setValues] = useState<SetupFormValues>(defaultValues);
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

    const port = Number.parseInt(values.ldap.port, 10);
    if (!Number.isFinite(port) || port < 1 || port > 65535) {
      errors["ldap.port"] = t("setup:validation.invalidPort");
    }

    if (!values.ldap.baseDn.trim()) {
      errors["ldap.baseDn"] = t("setup:validation.required");
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

    if (!values.admin.password.trim()) {
      errors["admin.password"] = t("setup:validation.required");
    }

    return errors;
  };

  const buildLdapPayload = (port: number) => ({
    name: values.ldap.name.trim() || "Default LDAP",
    host: values.ldap.host.trim(),
    port,
    useSsl: values.ldap.useSsl,
    baseDn: values.ldap.baseDn.trim(),
    userSearchBase: values.ldap.userSearchBase.trim(),
    userSearchFilter: values.ldap.userSearchFilter.trim(),
    bindUserName: values.ldap.bindUserName.trim(),
    bindUserDomain: emptyToNull(values.ldap.bindUserDomain),
    bindPassword: values.ldap.bindPassword,
    nationalIdAttribute: emptyToNull(values.ldap.nationalIdAttribute),
  });

  const buildCompleteRequest = (port: number): CompleteSetupRequest => ({
    setupKey: values.setupKey,
    ldap: buildLdapPayload(port),
    admin: {
      userName: values.admin.userName.trim(),
      password: values.admin.password,
    },
  });

  const handleCompleteSetup = async () => {
    const errors = validateForm();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    const port = Number.parseInt(values.ldap.port, 10);
    setIsSubmitting(true);
    const directoryUserNotFoundHint = t("setup:messages.directoryUserNotFoundHint");
    const directoryUserProfileHint = t("setup:messages.directoryUserProfileHint");
    const ldapTimeoutHint = t("setup:messages.ldapTimeoutHint");
    const completeFailedFallback = t("setup:messages.completeFailed");
    try {
      const response = await completeSetup(buildCompleteRequest(port));
      if (!response.isCompleted) {
        toast.error(
          mapCompleteSetupFailureToast(
            response.message,
            completeFailedFallback,
            directoryUserNotFoundHint,
            directoryUserProfileHint,
            ldapTimeoutHint,
          ),
        );
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
          toast.error(
            mapCompleteSetupFailureToast(
              data.message,
              completeFailedFallback,
              directoryUserNotFoundHint,
              directoryUserProfileHint,
              ldapTimeoutHint,
            ),
          );
          return;
        }
      }

      const fromApi = axios.isAxiosError(error)
        ? getApiErrorMessage(error, completeFailedFallback)
        : completeFailedFallback;

      toast.error(
        mapCompleteSetupFailureToast(
          fromApi,
          completeFailedFallback,
          directoryUserNotFoundHint,
          directoryUserProfileHint,
          ldapTimeoutHint,
        ),
      );
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

              <div className="space-y-2">
                <Label htmlFor="ldapPort">{t("setup:fields.port")}</Label>
                <Input
                  id="ldapPort"
                  inputMode="numeric"
                  value={values.ldap.port}
                  onChange={(event) => updateLdap("port", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldError message={fieldErrors["ldap.port"]} />
              </div>

              <div className="flex flex-col gap-2 rounded-md border border-border/60 bg-muted/20 p-3 md:col-span-2">
                <div className="flex items-start gap-3">
                  <Checkbox
                    id="ldapUseSsl"
                    className="mt-0.5"
                    checked={values.ldap.useSsl}
                    onChange={(event) => {
                      const nextSsl = event.target.checked;
                      setValues((current) => ({
                        ...current,
                        ldap: {
                          ...current.ldap,
                          useSsl: nextSsl,
                          port: portAfterSslToggle(nextSsl, current.ldap.port),
                        },
                      }));
                    }}
                    disabled={isSubmitting}
                  />
                  <Label htmlFor="ldapUseSsl" className="cursor-pointer font-normal leading-none">
                    {t("setup:fields.useSsl")}
                  </Label>
                </div>
                <FieldHint>{t("setup:helpers.ldapSslPort")}</FieldHint>
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

              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="ldapNationalIdAttribute">
                  {t("setup:fields.nationalIdAttribute")}
                </Label>
                <Input
                  id="ldapNationalIdAttribute"
                  value={values.ldap.nationalIdAttribute}
                  onChange={(event) => updateLdap("nationalIdAttribute", event.target.value)}
                  disabled={isSubmitting}
                />
              </div>
            </div>
          </SectionCard>

          <SectionCard title={t("setup:sections.admin")}>
            <div className="grid gap-4 sm:grid-cols-2">
              <p className="text-sm text-muted-foreground sm:col-span-2">
                {t("setup:helpers.adminCredentials")}
              </p>

              <div className="space-y-2">
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

              <div className="space-y-2">
                <Label htmlFor="adminPassword">{t("setup:fields.adminPassword")}</Label>
                <Input
                  id="adminPassword"
                  type="password"
                  autoComplete="new-password"
                  value={values.admin.password}
                  onChange={(event) => updateAdmin("password", event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <FieldError message={fieldErrors["admin.password"]} />
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
