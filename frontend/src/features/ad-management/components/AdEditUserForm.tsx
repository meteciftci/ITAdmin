import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { buildChangedMappedAttributes } from "@/features/ad-management/ad-edit-mapped-attributes";
import {
  buildDisplayNameFromParts,
  isSamAccountNameValid,
  isUserPrincipalNameValid,
} from "@/features/ad-management/ad-edit-user-validation";
import {
  AD_MANAGEMENT_USERS_QUERY_KEY,
  invalidateAdManagementUserQueries,
  updateAdUser,
} from "@/features/ad-management/api";
import type {
  AdUserDetail,
  MappedAdUserAttribute,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

type MappedFieldValues = Record<string, string>;
type FormErrors = Partial<
  Record<
    | "givenName"
    | "surname"
    | "displayName"
    | "samAccountName"
    | "userPrincipalName",
    string
  >
>;

function isMaskedPlaceholderValue(values: string[] | null | undefined): boolean {
  if (!values?.length) {
    return false;
  }

  return values.every((value) => value.trim() === "••••" || value.trim() === "");
}

function buildMappedInitialValues(attributes: MappedAdUserAttribute[]): MappedFieldValues {
  const initial: MappedFieldValues = {};
  for (const attribute of attributes) {
    if (!attribute.isEditable || isMaskedPlaceholderValue(attribute.value)) {
      initial[attribute.logicalField] = "";
      continue;
    }

    initial[attribute.logicalField] = attribute.value?.[0]?.trim() ?? "";
  }

  return initial;
}

type Props = {
  user: AdUserDetail;
  returnPath: string;
};

export function AdEditUserForm({ user, returnPath }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const editableMappedAttributes = useMemo(
    () => user.mappedAttributes.filter((attribute) => attribute.isEditable),
    [user.mappedAttributes],
  );

  const [givenName, setGivenName] = useState(user.givenName?.trim() ?? "");
  const [surname, setSurname] = useState(user.surname?.trim() ?? "");
  const [displayName, setDisplayName] = useState(user.displayName?.trim() ?? "");
  const [samAccountName, setSamAccountName] = useState(user.samAccountName?.trim() ?? "");
  const [userPrincipalName, setUserPrincipalName] = useState(
    user.userPrincipalName?.trim() ?? "",
  );
  const [mail, setMail] = useState(user.mail?.trim() ?? "");
  const [department, setDepartment] = useState(user.department?.trim() ?? "");
  const [mappedValues, setMappedValues] = useState<MappedFieldValues>(() =>
    buildMappedInitialValues(editableMappedAttributes),
  );
  const [formErrors, setFormErrors] = useState<FormErrors>({});

  const updateMutation = useMutation({
    mutationFn: (payload: Parameters<typeof updateAdUser>[1]) =>
      updateAdUser(user.id, payload),
    onSuccess: async () => {
      await invalidateAdManagementUserQueries(queryClient);
      await queryClient.invalidateQueries({
        queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", user.id],
      });
      toast.success(t("adManagement:users.edit.messages.updated"));
      navigate(returnPath);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.edit.messages.updateFailed")),
      );
    },
  });

  function applyNameChange(nextGivenName: string, nextSurname: string) {
    setGivenName(nextGivenName);
    setSurname(nextSurname);
    setDisplayName(buildDisplayNameFromParts(nextGivenName, nextSurname));
  }

  function validateForm(): boolean {
    const nextErrors: FormErrors = {};

    if (!givenName.trim()) {
      nextErrors.givenName = t("adManagement:users.edit.validation.givenNameRequired");
    }

    if (!surname.trim()) {
      nextErrors.surname = t("adManagement:users.edit.validation.surnameRequired");
    }

    if (!displayName.trim()) {
      nextErrors.displayName = t("adManagement:users.edit.validation.displayNameRequired");
    }

    if (!samAccountName.trim()) {
      nextErrors.samAccountName = t("adManagement:users.edit.validation.samAccountNameRequired");
    } else if (samAccountName.trim().length > 20) {
      nextErrors.samAccountName = t("adManagement:users.edit.validation.samAccountNameTooLong");
    } else if (!isSamAccountNameValid(samAccountName)) {
      nextErrors.samAccountName = t("adManagement:users.edit.validation.samAccountNameInvalid");
    }

    if (!userPrincipalName.trim()) {
      nextErrors.userPrincipalName = t("adManagement:users.edit.validation.upnRequired");
    } else if (!isUserPrincipalNameValid(userPrincipalName)) {
      nextErrors.userPrincipalName = t("adManagement:users.edit.validation.upnInvalid");
    }

    setFormErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  }

  function handleSubmit() {
    if (!validateForm()) {
      return;
    }

    const mappedAttributes = buildChangedMappedAttributes(
      editableMappedAttributes,
      mappedValues,
    );

    updateMutation.mutate({
      givenName: givenName.trim(),
      surname: surname.trim(),
      displayName: displayName.trim(),
      samAccountName: samAccountName.trim(),
      userPrincipalName: userPrincipalName.trim(),
      mail: mail.trim() || null,
      department: department.trim() || null,
      mappedAttributes,
    });
  }

  return (
    <SectionCard>
      <div className="space-y-6">
        <FormSection title={t("adManagement:users.edit.sections.basic")}>
          <Field
            label={t("adManagement:users.detail.givenName")}
            value={givenName}
            onChange={(value) => applyNameChange(value, surname)}
            required
            error={formErrors.givenName}
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:users.detail.surname")}
            value={surname}
            onChange={(value) => applyNameChange(givenName, value)}
            required
            error={formErrors.surname}
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:users.detail.displayName")}
            value={displayName}
            onChange={setDisplayName}
            required
            error={formErrors.displayName}
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:users.detail.department")}
            value={department}
            onChange={setDepartment}
            disabled={updateMutation.isPending}
          />
        </FormSection>

        <FormSection title={t("adManagement:users.edit.sections.account")}>
          <Field
            label={t("adManagement:users.detail.username")}
            value={samAccountName}
            onChange={setSamAccountName}
            required
            maxLength={20}
            error={formErrors.samAccountName}
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:users.detail.upn")}
            value={userPrincipalName}
            onChange={setUserPrincipalName}
            required
            error={formErrors.userPrincipalName}
            disabled={updateMutation.isPending}
          />
        </FormSection>

        <FormSection title={t("adManagement:users.edit.sections.contact")}>
          <Field
            label={t("adManagement:users.detail.email")}
            value={mail}
            onChange={setMail}
            type="email"
            disabled={updateMutation.isPending}
          />
        </FormSection>

        {editableMappedAttributes.length > 0 ? (
          <FormSection title={t("adManagement:users.edit.sections.mappedAttributes")}>
            {editableMappedAttributes.map((attribute) => (
              <MappedAttributeField
                key={`${attribute.logicalField}-${attribute.adAttribute}`}
                attribute={attribute}
                value={mappedValues[attribute.logicalField] ?? ""}
                onChange={(value) =>
                  setMappedValues((prev) => ({
                    ...prev,
                    [attribute.logicalField]: value,
                  }))
                }
                disabled={updateMutation.isPending}
              />
            ))}
          </FormSection>
        ) : null}

        <div className="flex flex-wrap justify-end gap-2">
          <Link
            to={returnPath}
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            {t("common:actions.cancel")}
          </Link>
          <Button type="button" onClick={handleSubmit} disabled={updateMutation.isPending}>
            {updateMutation.isPending
              ? t("common:actions.save")
              : t("adManagement:users.edit.actions.save")}
          </Button>
        </div>
      </div>
    </SectionCard>
  );
}

function FormSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <h3 className="text-sm font-semibold">{title}</h3>
      <div className="grid gap-3 md:grid-cols-2">{children}</div>
    </section>
  );
}

function Field({
  label,
  value,
  onChange,
  required,
  type = "text",
  maxLength,
  error,
  disabled,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  type?: string;
  maxLength?: number;
  error?: string;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-1.5">
      <Label>
        {label}
        {required ? " *" : ""}
      </Label>
      <Input
        value={value}
        type={type}
        maxLength={maxLength}
        onChange={(event) => onChange(event.target.value)}
        required={required}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        className="h-10"
      />
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
    </div>
  );
}

function MappedAttributeField({
  attribute,
  value,
  onChange,
  disabled,
}: {
  attribute: MappedAdUserAttribute;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const inputType =
    attribute.isSensitive ? "password" : attribute.adAttribute === "mail" ? "email" : "text";

  return (
    <div className="space-y-1.5">
      <Label>{attribute.displayName}</Label>
      <Input
        value={value}
        type={inputType}
        onChange={(event) => onChange(event.target.value)}
        disabled={disabled}
        className="h-10"
        autoComplete={attribute.isSensitive ? "new-password" : undefined}
      />
    </div>
  );
}
