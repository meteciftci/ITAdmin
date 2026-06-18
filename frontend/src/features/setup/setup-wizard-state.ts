import {
  isAdManagementModuleValid,
  isLdapFormComplete,
  type SetupWizardFormValues,
} from "./setup-form.ts";

export const SETUP_WIZARD_STEPS = [
  "setupKey",
  "serverCheck",
  "ldapConnection",
  "modules",
  "adminUsers",
  "summary",
] as const;

export type SetupWizardStep = (typeof SETUP_WIZARD_STEPS)[number];

export type SetupPreflightSnapshot = {
  canContinue: boolean;
};

export function getNextWizardStep(step: SetupWizardStep): SetupWizardStep | null {
  const index = SETUP_WIZARD_STEPS.indexOf(step);
  if (index < 0 || index >= SETUP_WIZARD_STEPS.length - 1) {
    return null;
  }

  return SETUP_WIZARD_STEPS[index + 1] ?? null;
}

export function getPreviousWizardStep(step: SetupWizardStep): SetupWizardStep | null {
  const index = SETUP_WIZARD_STEPS.indexOf(step);
  if (index <= 0) {
    return null;
  }

  return SETUP_WIZARD_STEPS[index - 1] ?? null;
}

export type SetupWizardNavigationContext = {
  values: SetupWizardFormValues;
  preflight: SetupPreflightSnapshot | null;
  ldapValidated: boolean;
};

export function canProceedFromWizardStep(
  step: SetupWizardStep,
  context: SetupWizardNavigationContext,
): boolean {
  switch (step) {
    case "setupKey":
      return context.values.setupKey.trim().length > 0;
    case "serverCheck":
      return context.preflight?.canContinue === true;
    case "ldapConnection":
      return isLdapFormComplete(context.values.ldap) && context.ldapValidated;
    case "modules":
      return isAdManagementModuleValid(context.values.modules);
    case "adminUsers":
      return context.values.adminUsers.length > 0;
    case "summary":
      return (
        context.values.setupKey.trim().length > 0 &&
        isLdapFormComplete(context.values.ldap) &&
        context.ldapValidated &&
        isAdManagementModuleValid(context.values.modules) &&
        context.values.adminUsers.length > 0 &&
        context.preflight?.canContinue === true
      );
    default:
      return false;
  }
}

export function isLdapDependentStep(step: SetupWizardStep): boolean {
  return step === "modules" || step === "adminUsers";
}

export function resolvePreflightMessageKey(messageKey: string): string {
  return messageKey.startsWith("setupPreflight.") ? messageKey : `setupPreflight.${messageKey}`;
}
