import { cn } from "@/lib/utils";

import type { SetupWizardStep } from "@/features/setup/setup-wizard-state";
import { SETUP_WIZARD_STEPS } from "@/features/setup/setup-wizard-state";

type SetupWizardStepperProps = {
  currentStep: SetupWizardStep;
  stepLabels: Record<SetupWizardStep, string>;
};

export function SetupWizardStepper({ currentStep, stepLabels }: SetupWizardStepperProps) {
  const currentIndex = SETUP_WIZARD_STEPS.indexOf(currentStep);

  return (
    <ol className="grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
      {SETUP_WIZARD_STEPS.map((step, index) => {
        const isActive = step === currentStep;
        const isComplete = index < currentIndex;

        return (
          <li
            key={step}
            className={cn(
              "rounded-lg border px-3 py-2 text-xs",
              isActive && "border-primary bg-primary/5 text-foreground",
              isComplete && !isActive && "border-border bg-muted/30 text-muted-foreground",
              !isActive && !isComplete && "border-border/70 text-muted-foreground",
            )}
          >
            <span className="block font-medium">{stepLabels[step]}</span>
          </li>
        );
      })}
    </ol>
  );
}
