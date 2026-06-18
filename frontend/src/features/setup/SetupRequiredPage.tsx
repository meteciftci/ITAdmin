import axios from "axios";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
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
import { completeSetup, getSetupStatus } from "@/features/setup/api";
import { SetupWizardStepper } from "@/features/setup/components/SetupWizardStepper";
import {
  AdminUsersStep,
  LdapConnectionStep,
  ModulesStep,
  ServerCheckStep,
  SetupKeyStep,
  SummaryStep,
  useServerCheckPreflight,
} from "@/features/setup/components/SetupWizardSteps";
import {
  buildCompleteSetupRequest,
  createDefaultSetupFormValues,
  isLdapFormComplete,
  mapCompleteSetupFailureToast,
  resolveResponseMessage,
  type SetupWizardFormValues,
} from "@/features/setup/setup-form";
import {
  canProceedFromWizardStep,
  getNextWizardStep,
  getPreviousWizardStep,
  SETUP_WIZARD_STEPS,
  type SetupWizardStep,
} from "@/features/setup/setup-wizard-state";
import { getApiErrorMessage } from "@/lib/api-error";

const SETUP_STATUS_QUERY_KEY = ["setup", "status"] as const;

export function SetupRequiredPage() {
  const { t } = useTranslation(["setup", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [currentStep, setCurrentStep] = useState<SetupWizardStep>("setupKey");
  const [values, setValues] = useState<SetupWizardFormValues>(() =>
    createDefaultSetupFormValues(t("setup:defaults.connectionName")),
  );
  const [ldapValidated, setLdapValidated] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const setupQuery = useQuery({
    queryKey: SETUP_STATUS_QUERY_KEY,
    queryFn: getSetupStatus,
  });

  const shouldLoadPreflight = currentStep === "serverCheck" || currentStep === "summary";
  const { preflight, isLoading: isPreflightLoading, errorMessage: preflightError, reloadPreflight } =
    useServerCheckPreflight(shouldLoadPreflight);

  const stepLabels = useMemo(
    () =>
      Object.fromEntries(
        SETUP_WIZARD_STEPS.map((step) => [step, t(`setup:wizardSteps.${step}`)]),
      ) as Record<SetupWizardStep, string>,
    [t],
  );

  const navigationContext = useMemo(
    () => ({
      values,
      preflight,
      ldapValidated,
    }),
    [values, preflight, ldapValidated],
  );

  const canProceed = canProceedFromWizardStep(currentStep, navigationContext);
  const previousStep = getPreviousWizardStep(currentStep);
  const nextStep = getNextWizardStep(currentStep);

  const handleCompleteSetup = async () => {
    if (isSubmitting) {
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
      toast.success(resolveResponseMessage(response.message, t("setup:messages.completeSuccess")));
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

  const ldapFieldErrors = useMemo(() => {
    const errors: Partial<Record<string, string>> = {};
    if (!values.ldap.host.trim()) errors["ldap.host"] = t("setup:validation.required");
    if (!values.ldap.baseDn.trim()) errors["ldap.baseDn"] = t("setup:validation.required");
    if (!values.ldap.userSearchFilter.trim()) errors["ldap.userSearchFilter"] = t("setup:validation.required");
    if (!values.ldap.bindUserName.trim()) errors["ldap.bindUserName"] = t("setup:validation.required");
    if (!values.ldap.bindPassword.trim()) errors["ldap.bindPassword"] = t("setup:validation.required");
    return errors;
  }, [t, values.ldap]);

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
      <div className="mx-auto max-w-4xl space-y-6">
        <Card className="border-border/70 shadow-lg">
          <CardHeader className="space-y-4">
            <div className="space-y-2">
              <CardTitle className="text-2xl">{t("setup:title")}</CardTitle>
              <CardDescription>{t("setup:description")}</CardDescription>
            </div>
            <SetupWizardStepper currentStep={currentStep} stepLabels={stepLabels} />
          </CardHeader>
        </Card>

        {currentStep === "setupKey" ? (
          <SetupKeyStep
            setupKey={values.setupKey}
            onChange={(setupKey) => setValues((current) => ({ ...current, setupKey }))}
            disabled={isSubmitting}
            error={values.setupKey.trim() ? undefined : undefined}
          />
        ) : null}

        {currentStep === "serverCheck" ? (
          <ServerCheckStep
            preflight={preflight}
            isLoading={isPreflightLoading}
            errorMessage={preflightError}
            onRetry={() => void reloadPreflight()}
          />
        ) : null}

        {currentStep === "ldapConnection" ? (
          <LdapConnectionStep
            setupKey={values.setupKey}
            ldap={values.ldap}
            onChange={(ldap) => setValues((current) => ({ ...current, ldap }))}
            ldapValidated={ldapValidated}
            onValidatedChange={setLdapValidated}
            disabled={isSubmitting}
            fieldErrors={ldapFieldErrors}
          />
        ) : null}

        {currentStep === "modules" ? (
          <ModulesStep
            setupKey={values.setupKey}
            ldap={values.ldap}
            modules={values.modules}
            onChange={(modules) => setValues((current) => ({ ...current, modules }))}
            ldapValidated={ldapValidated}
            disabled={isSubmitting}
          />
        ) : null}

        {currentStep === "adminUsers" ? (
          <AdminUsersStep
            setupKey={values.setupKey}
            ldap={values.ldap}
            adminUsers={values.adminUsers}
            onChange={(adminUsers) => setValues((current) => ({ ...current, adminUsers }))}
            ldapValidated={ldapValidated}
            disabled={isSubmitting}
          />
        ) : null}

        {currentStep === "summary" ? <SummaryStep values={values} preflight={preflight} /> : null}

        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
          <Button
            type="button"
            variant="outline"
            disabled={!previousStep || isSubmitting}
            onClick={() => previousStep && setCurrentStep(previousStep)}
          >
            {t("setup:actions.back")}
          </Button>

          {currentStep === "summary" ? (
            <Button type="button" disabled={!canProceed || isSubmitting} onClick={() => void handleCompleteSetup()}>
              {isSubmitting ? t("setup:actions.submitting") : t("setup:actions.complete")}
            </Button>
          ) : (
            <Button
              type="button"
              disabled={!canProceed || isSubmitting || (currentStep === "ldapConnection" && !isLdapFormComplete(values.ldap))}
              onClick={() => nextStep && setCurrentStep(nextStep)}
            >
              {t("setup:actions.next")}
            </Button>
          )}
        </div>
      </div>

      <div className="fixed bottom-6 right-6 flex items-center gap-2">
        <ThemeToggle />
        <PublicLanguageSwitcher />
      </div>
    </main>
  );
}
