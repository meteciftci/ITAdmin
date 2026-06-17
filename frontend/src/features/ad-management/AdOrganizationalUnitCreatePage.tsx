import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "@/features/ad-management/ad-ous-list-path";
import {
  buildAdOrganizationalUnitDetailPath,
  readAdOrganizationalUnitCreateParentDn,
} from "@/features/ad-management/ad-ou-detail-path";
import { buildAdOrganizationalUnitsListReturnState } from "@/features/ad-management/ad-ous-return-path";
import {
  createAdOrganizationalUnit,
  invalidateAdOrganizationalUnitQueries,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { cn } from "@/lib/utils";

export function AdOrganizationalUnitCreatePage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const moduleStatus = useAdManagementModuleStatus();

  const initialParentDn = useMemo(
    () => readAdOrganizationalUnitCreateParentDn(searchParams),
    [searchParams],
  );

  const [name, setName] = useState("");
  const [parentDistinguishedName, setParentDistinguishedName] = useState<string | null>(
    initialParentDn,
  );

  const createMutation = useMutation({
    mutationFn: () => {
      if (!parentDistinguishedName?.trim()) {
        throw new Error("Missing parent OU");
      }

      return createAdOrganizationalUnit({
        name: name.trim(),
        parentDistinguishedName: parentDistinguishedName.trim(),
      });
    },
    onSuccess: async (response) => {
      if (!response.success || !response.organizationalUnit) {
        toast.error(
          resolveAdManagementApiMessage(
            t,
            response,
            "adManagement:organizationalUnits.create.messages.createFailed",
          ),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.create.messages.created"));
      navigate(buildAdOrganizationalUnitDetailPath(response.organizationalUnit.objectGuid), {
        state: buildAdOrganizationalUnitsListReturnState(),
      });
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:organizationalUnits.create.messages.createFailed",
        ),
      );
    },
  });

  const canSubmit =
    moduleStatus.isOperational
    && name.trim().length > 0
    && Boolean(parentDistinguishedName?.trim())
    && !createMutation.isPending;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }

    createMutation.mutate();
  };

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:organizationalUnits.create.pageTitle")}
          description={t("adManagement:organizationalUnits.create.description")}
          actions={
            <Link
              to={AD_ORGANIZATIONAL_UNITS_LIST_PATH}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        <SectionCard title={t("adManagement:organizationalUnits.create.formTitle")}>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="ou-create-name">{t("adManagement:organizationalUnits.fields.name")}</Label>
              <Input
                id="ou-create-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("adManagement:organizationalUnits.fields.namePlaceholder")}
              />
            </div>

            <AdOuSearchCombobox
              value={parentDistinguishedName}
              onChange={setParentDistinguishedName}
              searchContext="manage"
              fieldLabelKey="adManagement:organizationalUnits.fields.parent"
              placeholderKey="adManagement:organizationalUnits.fields.parentPlaceholder"
              searchKey="adManagement:organizationalUnits.fields.parentSearch"
              emptyKey="adManagement:organizationalUnits.empty.notFound"
              errorKey="adManagement:organizationalUnits.errors.loadFailed"
            />

            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" disabled={!canSubmit} onClick={handleSubmit}>
                {t("common:actions.create")}
              </Button>
              <Link
                to={AD_ORGANIZATIONAL_UNITS_LIST_PATH}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
            </div>
          </div>
        </SectionCard>
      </section>
    </AdManagementModuleStateGuard>
  );
}
