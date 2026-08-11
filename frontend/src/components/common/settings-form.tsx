import {
  cloneElement,
  isValidElement,
  useEffect,
  useState,
  type ReactElement,
  type ReactNode,
} from "react";
import { Eye, EyeOff, LockKeyhole } from "lucide-react";
import { useBlocker } from "react-router-dom";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { SectionCard } from "@/components/common/SectionCard";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

type SettingsSectionProps = {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
};

export function SettingsSection({
  title,
  description,
  actions,
  children,
  className,
}: SettingsSectionProps) {
  return (
    <div className={className}>
      <SectionCard title={title} description={description} actions={actions}>
        <div className="space-y-5">{children}</div>
      </SectionCard>
    </div>
  );
}

type SettingsFieldProps = {
  id: string;
  label: ReactNode;
  description?: ReactNode;
  error?: ReactNode;
  optional?: boolean;
  optionalLabel?: ReactNode;
  children: ReactNode;
  className?: string;
};

export function SettingsField({
  id,
  label,
  description,
  error,
  optional = false,
  optionalLabel = "Optional",
  children,
  className,
}: SettingsFieldProps) {
  const descriptionIds = [description ? `${id}-description` : null, error ? `${id}-error` : null]
    .filter(Boolean)
    .join(" ");
  const control = isValidElement(children)
    ? cloneElement(
        children as ReactElement<{ "aria-describedby"?: string; "aria-invalid"?: boolean }>,
        {
          "aria-describedby": [
            (children.props as { "aria-describedby"?: string })["aria-describedby"],
            descriptionIds,
          ]
            .filter(Boolean)
            .join(" ") || undefined,
          "aria-invalid":
            (children.props as { "aria-invalid"?: boolean })["aria-invalid"] ?? Boolean(error),
        },
      )
    : children;

  return (
    <div className={cn("space-y-2", className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <Label htmlFor={id}>{label}</Label>
        {optional ? <span className="text-xs text-muted-foreground">{optionalLabel}</span> : null}
      </div>
      {description ? (
        <p id={`${id}-description`} className="text-sm leading-5 text-muted-foreground">
          {description}
        </p>
      ) : null}
      {control}
      {error ? (
        <p id={`${id}-error`} role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}

type SecretInputProps = Omit<React.ComponentProps<typeof Input>, "type"> & {
  hasStoredValue?: boolean;
  storedLabel: string;
  storedHint: string;
  showLabel: string;
  hideLabel: string;
};

export function SecretInput({
  hasStoredValue = false,
  storedLabel,
  storedHint,
  showLabel,
  hideLabel,
  className,
  ...props
}: SecretInputProps) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="space-y-2">
      {hasStoredValue ? (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <Badge variant="secondary" className="gap-1">
            <LockKeyhole className="size-3" aria-hidden="true" />
            {storedLabel}
          </Badge>
          <span>{storedHint}</span>
        </div>
      ) : null}
      <div className="relative">
        <Input
          type={visible ? "text" : "password"}
          autoComplete="new-password"
          className={cn("pr-11", className)}
          {...props}
        />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="absolute right-1 top-1/2 size-8 -translate-y-1/2"
          onClick={() => setVisible((current) => !current)}
          disabled={props.disabled || props.readOnly}
          aria-label={visible ? hideLabel : showLabel}
        >
          {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
        </Button>
      </div>
    </div>
  );
}

type SettingsFormActionsProps = {
  state: "pristine" | "dirty" | "saving" | "saved" | "error";
  stateLabel: ReactNode;
  errorTitle?: ReactNode;
  errorMessage?: ReactNode;
  children: ReactNode;
};

export function SettingsFormActions({
  state,
  stateLabel,
  errorTitle,
  errorMessage,
  children,
}: SettingsFormActionsProps) {
  return (
    <div className="space-y-3 rounded-xl border bg-card p-4 shadow-sm">
      {state === "error" && errorMessage ? (
        <Alert variant="destructive">
          {errorTitle ? <AlertTitle>{errorTitle}</AlertTitle> : null}
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      ) : null}
      <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p
          role={state === "error" ? "alert" : "status"}
          aria-live="polite"
          className={cn(
            "text-sm",
            state === "dirty" ? "font-medium text-foreground" : "text-muted-foreground",
            state === "error" && "text-destructive",
          )}
        >
          {stateLabel}
        </p>
        <div className="flex flex-wrap items-center gap-2">{children}</div>
      </div>
    </div>
  );
}

type UnsavedChangesGuardProps = {
  when: boolean;
  title: ReactNode;
  description: ReactNode;
  leaveText: ReactNode;
  stayText: ReactNode;
};

export function UnsavedChangesGuard({
  when,
  title,
  description,
  leaveText,
  stayText,
}: UnsavedChangesGuardProps) {
  const blocker = useBlocker(when);

  useEffect(() => {
    if (!when) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [when]);

  return (
    <ConfirmDialog
      open={blocker.state === "blocked"}
      title={title}
      description={description}
      confirmText={leaveText}
      cancelText={stayText}
      variant="danger"
      onConfirm={() => blocker.proceed?.()}
      onOpenChange={(open) => {
        if (!open) blocker.reset?.();
      }}
    />
  );
}
