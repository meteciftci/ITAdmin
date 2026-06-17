import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getAdGroupScopeLabel, getAdGroupTypeLabel } from "@/features/ad-management/ad-group-labels";
import {
  invalidateAdManagementGroupQueries,
  updateAdGroup,
} from "@/features/ad-management/api";
import type { AdGroupDetail } from "@/features/ad-management/types";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
import { cn } from "@/lib/utils";

type Props = {
  group: AdGroupDetail;
  returnPath: string;
};

export function AdEditGroupForm({ group, returnPath }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [displayName, setDisplayName] = useState(group.displayName?.trim() ?? "");
  const [technicalName, setTechnicalName] = useState(group.cn?.trim() ?? group.name.trim());
  const [samAccountName, setSamAccountName] = useState(group.samAccountName?.trim() ?? "");
  const [description, setDescription] = useState(group.description?.trim() ?? "");

  const updateMutation = useMutation({
    mutationFn: (payload: Parameters<typeof updateAdGroup>[1]) =>
      updateAdGroup(group.id, payload),
    onSuccess: async () => {
      await invalidateAdManagementGroupQueries(queryClient);
      toast.success(t("adManagement:groups.edit.messages.updated"));
      navigate(returnPath);
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:groups.edit.messages.updateFailed",
        ),
      );
    },
  });

  const canSubmit =
    displayName.trim().length > 0
    && technicalName.trim().length > 0
    && samAccountName.trim().length > 0
    && !updateMutation.isPending;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }

    updateMutation.mutate({
      displayName: displayName.trim(),
      name: technicalName.trim(),
      samAccountName: samAccountName.trim(),
      description: description.trim() || null,
    });
  };

  return (
    <SectionCard title={t("adManagement:groups.edit.formTitle")}>
      <div className="space-y-6">
        <div className="grid gap-4 md:grid-cols-2">
          <Field
            label={t("adManagement:groups.create.fields.displayName")}
            value={displayName}
            onChange={setDisplayName}
            required
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:groups.create.fields.technicalName")}
            value={technicalName}
            onChange={setTechnicalName}
            required
            disabled={updateMutation.isPending}
          />
          <Field
            label={t("adManagement:groups.create.fields.samAccountName")}
            value={samAccountName}
            onChange={setSamAccountName}
            required
            disabled={updateMutation.isPending}
          />
          <ReadonlyField
            label={t("adManagement:groups.create.fields.scope")}
            value={getAdGroupScopeLabel(t, group.groupScope)}
          />
          <ReadonlyField
            label={t("adManagement:groups.table.type")}
            value={getAdGroupTypeLabel(t, group.securityEnabled)}
          />
          <ReadonlyField
            label={t("adManagement:groups.table.distinguishedName")}
            value={group.distinguishedName}
          />
        </div>

        <Field
          label={t("adManagement:groups.create.fields.description")}
          value={description}
          onChange={setDescription}
          disabled={updateMutation.isPending}
        />

        <div className="flex flex-wrap justify-end gap-2">
          <Link
            to={returnPath}
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            {t("common:actions.cancel")}
          </Link>
          <Button type="button" onClick={handleSubmit} disabled={!canSubmit}>
            {updateMutation.isPending
              ? t("common:actions.save")
              : t("adManagement:groups.edit.actions.submit")}
          </Button>
        </div>
      </div>
    </SectionCard>
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

function ReadonlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <Input value={value} readOnly disabled className="h-10 bg-muted/40" title={value} />
    </div>
  );
}
