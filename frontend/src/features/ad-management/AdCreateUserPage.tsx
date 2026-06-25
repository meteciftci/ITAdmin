import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { PageHeader } from "@/components/common/PageHeader";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import {
  buildAdUserPrincipalName,
  normalizeAdUsername,
} from "@/features/ad-management/ad-user-name";
import { resolveDefaultUpnSuffix } from "@/features/ad-management/resolve-default-upn-suffix";
import { resolveAdUserCreateTargetOu } from "@/features/ad-management/resolve-ad-create-target-ou";
import {
  AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  AD_UPN_SUFFIXES_QUERY_KEY,
  createAdUser,
  getAdAttributeMappings,
  getAdManagementSettings,
  getAdUpnSuffixes,
  invalidateAdManagementUserQueries,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdAttributeMapping,
  CreateAdUserMappedAttributeRequest,
} from "@/features/ad-management/types";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { cn } from "@/lib/utils";

type MappedFieldValues = Record<string, string>;

export function AdCreateUserPage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();

  const [givenName, setGivenName] = useState("");
  const [surname, setSurname] = useState("");
  const [department, setDepartment] = useState("");
  const [samAccountName, setSamAccountName] = useState("");
  const [upnSuffix, setUpnSuffix] = useState("");
  const [initialPassword, setInitialPassword] = useState("");
  const [selectedOuDistinguishedName, setSelectedOuDistinguishedName] = useState<string | null>(
    null,
  );
  const [isEnabled, setIsEnabled] = useState(true);
  const [mustChangePasswordAtNextLogon, setMustChangePasswordAtNextLogon] = useState(false);
  const [usernameTouched, setUsernameTouched] = useState(false);
  const [mappedValues, setMappedValues] = useState<MappedFieldValues>({});

  const settingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
  });

  const upnSuffixesQuery = useQuery({
    queryKey: AD_UPN_SUFFIXES_QUERY_KEY,
    queryFn: getAdUpnSuffixes,
    enabled: moduleStatus.isOperational && settingsQuery.isSuccess,
  });

  const mappingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
    queryFn: getAdAttributeMappings,
    enabled: moduleStatus.isOperational,
  });

  const effectiveTargetOu = resolveAdUserCreateTargetOu(
    selectedOuDistinguishedName,
    settingsQuery.data,
  );

  const suffixItems = useMemo(
    () => upnSuffixesQuery.data?.items ?? [],
    [upnSuffixesQuery.data?.items],
  );

  const autoSelectedUpnSuffix = useMemo(
    () =>
      resolveDefaultUpnSuffix(
        settingsQuery.data?.defaultUserCreationUpnSuffix,
        suffixItems,
      ),
    [settingsQuery.data?.defaultUserCreationUpnSuffix, suffixItems],
  );

  const effectiveUpnSuffix = upnSuffix || autoSelectedUpnSuffix;

  const editableMappings = useMemo(
    () =>
      (mappingsQuery.data ?? []).filter(
        (mapping) => mapping.isEnabled && mapping.isEditable,
      ),
    [mappingsQuery.data],
  );

  const autoUsername = useMemo(() => {
    if (!givenName.trim() || !surname.trim()) {
      return "";
    }

    return normalizeAdUsername(givenName, surname);
  }, [givenName, surname]);

  const effectiveUsername = usernameTouched ? samAccountName : autoUsername;
  const upnPreview =
    effectiveUsername && effectiveUpnSuffix
      ? buildAdUserPrincipalName(effectiveUsername, effectiveUpnSuffix)
      : "";

  const createMutation = useMutation({
    mutationFn: createAdUser,
    onSuccess: async (response) => {
      await invalidateAdManagementUserQueries(queryClient);
      const baseMessage = resolveAdManagementApiMessage(
        t,
        response,
        "adManagement:users.create.messages.created",
      );
      if ((response.notificationSummary?.queuedCount ?? 0) > 0) {
        toast.success(
          `${baseMessage} ${t("adManagement:users.create.messages.notificationQueued")}`,
        );
      } else if (
        (response.notificationSummary?.skippedCount ?? 0) > 0
        && (response.notificationSummary?.messages.length ?? 0) > 0
      ) {
        toast.success(baseMessage);
        toast.message(t("adManagement:users.create.messages.notificationNotQueued"));
      } else {
        toast.success(baseMessage);
      }
      navigate(AD_USERS_LIST_PATH);
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:users.create.messages.createFailed",
        ),
      );
    },
  });

  function handleSubmit() {
    if (!effectiveUpnSuffix.trim()) {
      toast.error(t("adManagement:users.create.errors.upnSuffixRequired"));
      return;
    }

    if (!effectiveTargetOu) {
      toast.error(t("adManagement:users.create.errors.ouRequired"));
      return;
    }

    const mappedAttributes: CreateAdUserMappedAttributeRequest[] = editableMappings
      .map((mapping) => ({
        logicalField: mapping.logicalField,
        value: mappedValues[mapping.logicalField]?.trim() || null,
      }))
      .filter((item) => item.value);

    createMutation.mutate({
      givenName: givenName.trim(),
      surname: surname.trim(),
      department: department.trim() || null,
      samAccountName: effectiveUsername.trim() || null,
      upnSuffix: effectiveUpnSuffix.trim(),
      targetOuDistinguishedName: effectiveTargetOu,
      initialPassword,
      isEnabled,
      mustChangePasswordAtNextLogon,
      mappedAttributes,
    });
  }

  const upnSuffixBlocking =
    upnSuffixesQuery.isLoading ||
    upnSuffixesQuery.isError ||
    (upnSuffixesQuery.isSuccess && suffixItems.length === 0);

  const canSubmit =
    moduleStatus.isOperational &&
    Boolean(givenName.trim()) &&
    Boolean(surname.trim()) &&
    Boolean(initialPassword.trim()) &&
    Boolean(effectiveTargetOu) &&
    Boolean(effectiveUpnSuffix.trim()) &&
    !upnSuffixBlocking &&
    !createMutation.isPending;

  return (
    <AdManagementModuleStateGuard>
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("adManagement:users.create.pageTitle")}
        description={t("adManagement:users.create.pageDescription")}
        actions={
          <Link
            to={AD_USERS_LIST_PATH}
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            {t("common:actions.back")}
          </Link>
        }
      />

      <SectionCard>
        {upnSuffixBlocking ? (
          <p className="text-sm text-destructive">
            {upnSuffixesQuery.isLoading
              ? t("common:loading")
              : t("adManagement:users.create.errors.upnSuffixLoadFailed")}
          </p>
        ) : (
          <div className="space-y-6">
            <FormSection title={t("adManagement:users.create.sections.basic")}>
              <Field
                label={t("adManagement:users.create.fields.givenName")}
                value={givenName}
                onChange={setGivenName}
                required
              />
              <Field
                label={t("adManagement:users.create.fields.surname")}
                value={surname}
                onChange={setSurname}
                required
              />
              <Field
                label={t("adManagement:users.create.fields.department")}
                value={department}
                onChange={setDepartment}
              />
            </FormSection>

            <FormSection title={t("adManagement:users.create.sections.account")}>
              <Field
                label={t("adManagement:users.create.fields.username")}
                value={effectiveUsername}
                onChange={(value) => {
                  setUsernameTouched(true);
                  setSamAccountName(value);
                }}
              />
              <div className="space-y-1.5">
                <Label>{t("adManagement:users.create.fields.upnSuffix")} *</Label>
                <Select
                  value={effectiveUpnSuffix}
                  onChange={(event) => setUpnSuffix(event.target.value)}
                  disabled={suffixItems.length === 0}
                  className="h-10"
                >
                  {suffixItems.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.value}
                    </option>
                  ))}
                </Select>
                <p className="text-xs text-muted-foreground">
                  {t("adManagement:users.create.fields.upnSuffixHelp")}
                </p>
                {upnSuffixesQuery.data?.warning ? (
                  <p className="text-xs text-amber-600 dark:text-amber-400">
                    {upnSuffixesQuery.data.warning}
                  </p>
                ) : null}
              </div>
              <ReadonlyField
                label={t("adManagement:users.create.fields.upnPreview")}
                value={upnPreview}
              />
              <Field
                label={t("adManagement:users.create.fields.initialPassword")}
                value={initialPassword}
                onChange={setInitialPassword}
                type="password"
                required
              />
            </FormSection>

            <AccountOrganizationSection
              isEnabled={isEnabled}
              onIsEnabledChange={setIsEnabled}
              mustChangePasswordAtNextLogon={mustChangePasswordAtNextLogon}
              onMustChangePasswordChange={setMustChangePasswordAtNextLogon}
              targetOu={effectiveTargetOu}
              onTargetOuChange={setSelectedOuDistinguishedName}
              disabled={!moduleStatus.isOperational || createMutation.isPending}
            />

            {editableMappings.length > 0 ? (
              <FormSection title={t("adManagement:users.create.sections.mappedAttributes")}>
                {editableMappings.map((mapping) => (
                  <MappedAttributeField
                    key={mapping.id}
                    mapping={mapping}
                    value={mappedValues[mapping.logicalField] ?? ""}
                    onChange={(value) =>
                      setMappedValues((prev) => ({
                        ...prev,
                        [mapping.logicalField]: value,
                      }))
                    }
                  />
                ))}
              </FormSection>
            ) : null}

            <div className="flex flex-wrap justify-end gap-2">
              <Link
                to={AD_USERS_LIST_PATH}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
              <Button type="button" onClick={handleSubmit} disabled={!canSubmit}>
                {createMutation.isPending
                  ? t("common:actions.save")
                  : t("adManagement:users.create.actions.submit")}
              </Button>
            </div>
          </div>
        )}
      </SectionCard>
    </section>
    </AdManagementModuleStateGuard>
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
  disabled,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  type?: string;
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
        onChange={(event) => onChange(event.target.value)}
        required={required}
        disabled={disabled}
        className="h-10"
      />
    </div>
  );
}

function ReadonlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <Input value={value} readOnly disabled className="h-10 bg-muted/40" />
    </div>
  );
}

function AccountOrganizationSection({
  isEnabled,
  onIsEnabledChange,
  mustChangePasswordAtNextLogon,
  onMustChangePasswordChange,
  targetOu,
  onTargetOuChange,
  disabled,
}: {
  isEnabled: boolean;
  onIsEnabledChange: (checked: boolean) => void;
  mustChangePasswordAtNextLogon: boolean;
  onMustChangePasswordChange: (checked: boolean) => void;
  targetOu: string | null;
  onTargetOuChange: (distinguishedName: string | null) => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation("adManagement");

  return (
    <section className="space-y-3">
      <h3 className="text-sm font-semibold">
        {t("users.create.sections.accountOrganization")}
      </h3>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-[minmax(220px,auto)_minmax(260px,auto)_minmax(360px,1fr)]">
        <OptionCard title={t("users.create.cards.accountStatus")}>
          <CheckboxOption
            id="ad-create-enabled"
            label={t("users.create.fields.isEnabled")}
            checked={isEnabled}
            onChange={onIsEnabledChange}
            disabled={disabled}
          />
        </OptionCard>
        <OptionCard title={t("users.create.cards.passwordPolicy")}>
          <CheckboxOption
            id="ad-create-must-change-password"
            label={t("users.create.fields.mustChangePasswordAtNextLogon")}
            checked={mustChangePasswordAtNextLogon}
            onChange={onMustChangePasswordChange}
            disabled={disabled}
          />
        </OptionCard>
        <OptionCard
          title={t("users.create.cards.organizationalUnit")}
          className="md:col-span-2 xl:col-span-1"
        >
          <AdOuSearchCombobox
            value={targetOu}
            onChange={onTargetOuChange}
            disabled={disabled}
            showFieldLabel={false}
            className="w-full"
          />
        </OptionCard>
      </div>
    </section>
  );
}

function OptionCard({
  title,
  children,
  className,
}: {
  title: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex min-h-[7.5rem] flex-col gap-3 rounded-lg border border-border bg-muted/20 p-4",
        className,
      )}
    >
      <p className="text-sm font-medium">{title}</p>
      <div className="mt-auto">{children}</div>
    </div>
  );
}

function CheckboxOption({
  id,
  label,
  checked,
  onChange,
  disabled,
}: {
  id: string;
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex items-start gap-2">
      <Checkbox
        id={id}
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        disabled={disabled}
      />
      <label htmlFor={id} className="cursor-pointer text-sm leading-snug">
        {label}
      </label>
    </div>
  );
}

function MappedAttributeField({
  mapping,
  value,
  onChange,
  disabled,
}: {
  mapping: AdAttributeMapping;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const inputType = mapping.isSensitive
    ? "password"
    : mapping.validationType === "Email"
      ? "email"
      : "text";

  return (
    <div className="space-y-1.5">
      <Label>{mapping.displayName}</Label>
      <Input
        value={value}
        type={inputType}
        onChange={(event) => onChange(event.target.value)}
        disabled={disabled}
        className="h-10"
      />
    </div>
  );
}
