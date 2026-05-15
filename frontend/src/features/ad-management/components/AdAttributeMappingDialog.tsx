import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import {
  AD_ATTRIBUTE_NAME_REGEX,
  AD_LOGICAL_FIELD_REGEX,
  AD_MASKING_STRATEGIES,
  AD_VALIDATION_TYPES,
  type AdAttributeMapping,
} from "@/features/ad-management/types";

export type AdAttributeMappingDialogFormState = {
  logicalField: string;
  displayName: string;
  attributeName: string;
  isEnabled: boolean;
  isEditable: boolean;
  isSensitive: boolean;
  validationType: string;
  maskingStrategy: string;
  sortOrder: number;
};

type Props = {
  open: boolean;
  mode: "create" | "edit";
  initialValue: AdAttributeMapping | null;
  isSaving: boolean;
  errorMessage?: string | null;
  onSubmit: (value: AdAttributeMappingDialogFormState) => void;
  onOpenChange: (open: boolean) => void;
};

function buildInitialState(
  initialValue: AdAttributeMapping | null,
): AdAttributeMappingDialogFormState {
  if (!initialValue) {
    return {
      logicalField: "",
      displayName: "",
      attributeName: "",
      isEnabled: true,
      isEditable: true,
      isSensitive: false,
      validationType: "None",
      maskingStrategy: "None",
      sortOrder: 0,
    };
  }

  return {
    logicalField: initialValue.logicalField,
    displayName: initialValue.displayName,
    attributeName: initialValue.attributeName,
    isEnabled: initialValue.isEnabled,
    isEditable: initialValue.isEditable,
    isSensitive: initialValue.isSensitive,
    validationType: initialValue.validationType,
    maskingStrategy: initialValue.maskingStrategy,
    sortOrder: initialValue.sortOrder,
  };
}

export function AdAttributeMappingDialog(props: Props) {
  if (!props.open) {
    return null;
  }

  return <DialogContents {...props} />;
}

function DialogContents({
  mode,
  initialValue,
  isSaving,
  errorMessage,
  onSubmit,
  onOpenChange,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [state, setState] = useState<AdAttributeMappingDialogFormState>(
    () => buildInitialState(initialValue),
  );
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<keyof AdAttributeMappingDialogFormState, string>>
  >({});

  const isCreate = mode === "create";

  const title = useMemo(
    () =>
      isCreate
        ? t("settings:adManagement.mappings.create.title")
        : t("settings:adManagement.mappings.edit.title"),
    [isCreate, t],
  );

  function validate(): boolean {
    const next: Partial<Record<keyof AdAttributeMappingDialogFormState, string>> = {};

    if (isCreate) {
      const trimmed = state.logicalField.trim();
      if (!trimmed) {
        next.logicalField = t("settings:adManagement.mappings.validation.logicalFieldRequired");
      } else if (!AD_LOGICAL_FIELD_REGEX.test(trimmed)) {
        next.logicalField = t("settings:adManagement.mappings.validation.logicalFieldInvalid");
      }
    }

    if (!state.displayName.trim()) {
      next.displayName = t("settings:adManagement.mappings.validation.displayNameRequired");
    }

    const attributeName = state.attributeName.trim();
    if (!attributeName) {
      next.attributeName = t("settings:adManagement.mappings.validation.attributeNameRequired");
    } else if (!AD_ATTRIBUTE_NAME_REGEX.test(attributeName)) {
      next.attributeName = t("settings:adManagement.mappings.validation.attributeNameInvalid");
    }

    setFieldErrors(next);
    return Object.keys(next).length === 0;
  }

  function handleSubmit() {
    if (!validate()) return;

    onSubmit({
      ...state,
      logicalField: state.logicalField.trim(),
      displayName: state.displayName.trim(),
      attributeName: state.attributeName.trim(),
    });
  }

  function updateField<K extends keyof AdAttributeMappingDialogFormState>(
    field: K,
    value: AdAttributeMappingDialogFormState[K],
  ) {
    setState((prev) => ({ ...prev, [field]: value }));
    setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
  }

  return (
    <Dialog open>
      <DialogContent onOpenChange={onOpenChange} className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 p-4">
          <FormError message={errorMessage} />
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-logical-field">
                {t("settings:adManagement.mappings.fields.logicalField")}
                <span className="text-destructive">*</span>
              </Label>
              <Input
                id="ad-mapping-logical-field"
                value={state.logicalField}
                onChange={(event) => updateField("logicalField", event.target.value)}
                readOnly={!isCreate}
                aria-invalid={Boolean(fieldErrors.logicalField)}
                placeholder="mobilePhone"
              />
              {fieldErrors.logicalField ? (
                <p className="text-xs text-destructive">{fieldErrors.logicalField}</p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  {t("settings:adManagement.mappings.fields.logicalFieldHelp")}
                </p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-display-name">
                {t("settings:adManagement.mappings.fields.displayName")}
                <span className="text-destructive">*</span>
              </Label>
              <Input
                id="ad-mapping-display-name"
                value={state.displayName}
                onChange={(event) => updateField("displayName", event.target.value)}
                aria-invalid={Boolean(fieldErrors.displayName)}
                maxLength={150}
              />
              {fieldErrors.displayName ? (
                <p className="text-xs text-destructive">{fieldErrors.displayName}</p>
              ) : null}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-attribute-name">
                {t("settings:adManagement.mappings.fields.attributeName")}
                <span className="text-destructive">*</span>
              </Label>
              <Input
                id="ad-mapping-attribute-name"
                value={state.attributeName}
                onChange={(event) => updateField("attributeName", event.target.value)}
                aria-invalid={Boolean(fieldErrors.attributeName)}
                placeholder="extensionAttribute1"
              />
              {fieldErrors.attributeName ? (
                <p className="text-xs text-destructive">{fieldErrors.attributeName}</p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  {t("settings:adManagement.mappings.fields.attributeNameHelp")}
                </p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-sort-order">
                {t("settings:adManagement.mappings.fields.sortOrder")}
              </Label>
              <Input
                id="ad-mapping-sort-order"
                type="number"
                min={0}
                max={9999}
                value={Number.isFinite(state.sortOrder) ? state.sortOrder : 0}
                onChange={(event) => {
                  const parsed = Number.parseInt(event.target.value, 10);
                  updateField("sortOrder", Number.isFinite(parsed) ? parsed : 0);
                }}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-validation-type">
                {t("settings:adManagement.mappings.fields.validationType")}
              </Label>
              <Select
                id="ad-mapping-validation-type"
                value={state.validationType}
                onChange={(event) => updateField("validationType", event.target.value)}
              >
                {AD_VALIDATION_TYPES.map((value) => (
                  <option key={value} value={value}>
                    {t(`settings:adManagement.mappings.validationTypes.${value}`)}
                  </option>
                ))}
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ad-mapping-masking-strategy">
                {t("settings:adManagement.mappings.fields.maskingStrategy")}
              </Label>
              <Select
                id="ad-mapping-masking-strategy"
                value={state.maskingStrategy}
                onChange={(event) => updateField("maskingStrategy", event.target.value)}
              >
                {AD_MASKING_STRATEGIES.map((value) => (
                  <option key={value} value={value}>
                    {t(`settings:adManagement.mappings.maskingStrategies.${value}`)}
                  </option>
                ))}
              </Select>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-3">
            <ToggleField
              id="ad-mapping-is-enabled"
              label={t("settings:adManagement.mappings.fields.isEnabled")}
              checked={state.isEnabled}
              onChange={(checked) => updateField("isEnabled", checked)}
            />
            <ToggleField
              id="ad-mapping-is-editable"
              label={t("settings:adManagement.mappings.fields.isEditable")}
              checked={state.isEditable}
              onChange={(checked) => updateField("isEditable", checked)}
            />
            <ToggleField
              id="ad-mapping-is-sensitive"
              label={t("settings:adManagement.mappings.fields.isSensitive")}
              checked={state.isSensitive}
              onChange={(checked) => updateField("isSensitive", checked)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isSaving}>
            {t("common:actions.cancel")}
          </Button>
          <Button onClick={handleSubmit} disabled={isSaving}>
            {t("common:actions.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ToggleField({
  id,
  label,
  checked,
  onChange,
}: {
  id: string;
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div className="flex items-center gap-2">
      <Checkbox
        id={id}
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <label htmlFor={id} className="cursor-pointer text-sm">
        {label}
      </label>
    </div>
  );
}
