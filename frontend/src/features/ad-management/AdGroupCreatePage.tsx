import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { AD_GROUPS_LIST_PATH } from "@/features/ad-management/ad-groups-list-path";
import { buildAdGroupsListReturnState } from "@/features/ad-management/ad-groups-return-path";
import { buildAdGroupDetailPath } from "@/features/ad-management/ad-group-detail-path";
import { buildAdGroupSamAccountNameSuggestion } from "@/features/ad-management/ad-group-name";
import {
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  createAdGroup,
  getAdManagementSettings,
  invalidateAdManagementGroupQueries,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type { AdGroupScope } from "@/features/ad-management/types";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
import { resolveAdGroupCreateTargetOu } from "@/features/ad-management/resolve-ad-create-target-ou";
import { cn } from "@/lib/utils";

const GROUP_SCOPE_OPTIONS: AdGroupScope[] = ["Global", "DomainLocal", "Universal"];

export function AdGroupCreatePage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();

  const [displayName, setDisplayName] = useState("");
  const [technicalName, setTechnicalName] = useState("");
  const [samAccountName, setSamAccountName] = useState("");
  const [description, setDescription] = useState("");
  const [groupScope, setGroupScope] = useState<AdGroupScope>("Global");
  const [selectedOuDistinguishedName, setSelectedOuDistinguishedName] = useState<string | null>(
    null,
  );
  const [samAccountNameTouched, setSamAccountNameTouched] = useState(false);

  const settingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
  });

  const effectiveTargetOu = resolveAdGroupCreateTargetOu(
    selectedOuDistinguishedName,
    settingsQuery.data,
  );

  const autoSamAccountName = useMemo(() => {
    if (!technicalName.trim()) {
      return "";
    }

    return buildAdGroupSamAccountNameSuggestion(technicalName);
  }, [technicalName]);

  const effectiveSamAccountName = samAccountNameTouched ? samAccountName : autoSamAccountName;

  const createMutation = useMutation({
    mutationFn: createAdGroup,
    onSuccess: async (group) => {
      await invalidateAdManagementGroupQueries(queryClient);
      toast.success(t("adManagement:groups.create.messages.created"));
      navigate(buildAdGroupDetailPath(group.id), {
        state: buildAdGroupsListReturnState(),
      });
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:groups.create.messages.createFailed",
        ),
      );
    },
  });

  const canSubmit =
    moduleStatus.isOperational
    && displayName.trim().length > 0
    && technicalName.trim().length > 0
    && effectiveSamAccountName.trim().length > 0
    && Boolean(effectiveTargetOu)
    && !createMutation.isPending;

  const handleSubmit = () => {
    if (!canSubmit || !effectiveTargetOu) {
      return;
    }

    createMutation.mutate({
      displayName: displayName.trim(),
      name: technicalName.trim(),
      samAccountName: effectiveSamAccountName.trim(),
      description: description.trim() || null,
      groupScope,
      targetOuDistinguishedName: effectiveTargetOu,
    });
  };

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:groups.create.pageTitle")}
          description={t("adManagement:groups.create.description")}
          actions={
            <Link
              to={AD_GROUPS_LIST_PATH}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        <SectionCard title={t("adManagement:groups.create.formTitle")}>
          <div className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2">
              <Field
                label={t("adManagement:groups.create.fields.displayName")}
                value={displayName}
                onChange={setDisplayName}
                required
              />
              <Field
                label={t("adManagement:groups.create.fields.technicalName")}
                value={technicalName}
                onChange={setTechnicalName}
                required
                disabled={createMutation.isPending}
              />
              <Field
                label={t("adManagement:groups.create.fields.samAccountName")}
                value={effectiveSamAccountName}
                onChange={(value) => {
                  if (!value.trim()) {
                    setSamAccountNameTouched(false);
                    setSamAccountName("");
                    return;
                  }

                  setSamAccountNameTouched(true);
                  setSamAccountName(value);
                }}
                required
                disabled={createMutation.isPending}
              />
              <div className="space-y-1.5">
                <Label>{t("adManagement:groups.create.fields.scope")} *</Label>
                <Select
                  value={groupScope}
                  onChange={(event) => setGroupScope(event.target.value as AdGroupScope)}
                  disabled={createMutation.isPending}
                  className="h-10"
                >
                  {GROUP_SCOPE_OPTIONS.map((scope) => (
                    <option key={scope} value={scope}>
                      {t(`adManagement:groups.scope.${scope === "DomainLocal" ? "domainLocal" : scope.toLowerCase()}`)}
                    </option>
                  ))}
                </Select>
              </div>
            </div>

            <Field
              label={t("adManagement:groups.create.fields.description")}
              value={description}
              onChange={setDescription}
              disabled={createMutation.isPending}
            />

            <AdOuSearchCombobox
              value={effectiveTargetOu}
              onChange={setSelectedOuDistinguishedName}
              searchContext="groups"
              showFieldLabel
              fieldLabelKey="adManagement:groups.create.fields.ou"
              placeholderKey="adManagement:groups.create.fields.ouPlaceholder"
              searchKey="adManagement:groups.create.fields.ouSearch"
              emptyKey="adManagement:groups.create.empty.ouNotFound"
              errorKey="adManagement:groups.create.errors.ouLoadFailed"
              disabled={createMutation.isPending}
            />

            <div className="flex flex-wrap justify-end gap-2">
              <Link
                to={AD_GROUPS_LIST_PATH}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
              <Button type="button" onClick={handleSubmit} disabled={!canSubmit}>
                {createMutation.isPending
                  ? t("common:actions.save")
                  : t("adManagement:groups.create.actions.submit")}
              </Button>
            </div>
          </div>
        </SectionCard>
      </section>
    </AdManagementModuleStateGuard>
  );
}

function Field({
  label,
  value,
  onChange,
  required,
  disabled,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
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
        onChange={(event) => onChange(event.target.value)}
        required={required}
        disabled={disabled}
        className="h-10"
      />
    </div>
  );
}
