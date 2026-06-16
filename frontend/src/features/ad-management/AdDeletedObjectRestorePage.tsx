import { useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";
import { toast } from "sonner";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AD_DELETED_OBJECTS_LIST_PATH } from "@/features/ad-management/ad-deleted-objects-list-query";
import { getAdDeletedObjectTypeLabel } from "@/features/ad-management/ad-deleted-object-labels";
import { canRestoreDeletedObject } from "@/features/ad-management/ad-deleted-object-restore-eligibility";
import type { AdDeletedObjectRestoreTargetMode } from "@/features/ad-management/ad-deleted-object-restore-types";
import {
  buildExpectedRestoredDistinguishedName,
  getAdDeletedObjectRestoreConfirmationValue,
  normalizeDeletedObjectRestoreRdn,
  resolveDeletedObjectOuSearchContext,
} from "@/features/ad-management/ad-deleted-object-restore-utils";
import { resolveAdDeletedObjectReturnPath } from "@/features/ad-management/ad-deleted-objects-return-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import {
  AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY,
  getAdDeletedObjectById,
  invalidateAdManagementDeletedObjectRestoreQueries,
  restoreAdDeletedObject,
} from "@/features/ad-management/api";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

function SummaryField({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: ReactNode;
  mono?: boolean;
}) {
  const isPrimitive = typeof value === "string" || value === null || value === undefined;
  const display = isPrimitive ? (value?.toString().trim() || "-") : value;

  return (
    <div className="space-y-1">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div
        className={cn("text-sm", mono && isPrimitive && "break-all font-mono text-xs text-muted-foreground")}
        title={isPrimitive ? display?.toString() : undefined}
      >
        {display}
      </div>
    </div>
  );
}

export function AdDeletedObjectRestorePage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const returnPath = resolveAdDeletedObjectReturnPath(location.state, AD_DELETED_OBJECTS_LIST_PATH);
  const [confirmValue, setConfirmValue] = useState("");
  const [restoreTargetMode, setRestoreTargetMode] =
    useState<AdDeletedObjectRestoreTargetMode>("OriginalLocation");
  const [targetPathDistinguishedName, setTargetPathDistinguishedName] = useState<string | null>(null);

  const hasValidId = Boolean(id?.trim()) && isGuidLike(id);

  const detailQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY, "detail", id],
    queryFn: () => getAdDeletedObjectById(id!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const detail = detailQuery.data;
  const confirmation = detail ? getAdDeletedObjectRestoreConfirmationValue(detail) : null;
  const expectedConfirmValue = confirmation?.value ?? "";
  const isConfirmMatch =
    expectedConfirmValue.length > 0
    && confirmValue.trim().toLowerCase() === expectedConfirmValue.toLowerCase();
  const isRestorable = detail ? canRestoreDeletedObject(detail) : false;

  const normalizedRestoreRdn = useMemo(
    () => normalizeDeletedObjectRestoreRdn(detail?.lastKnownRdn),
    [detail?.lastKnownRdn],
  );

  const selectedRestoreParentDn = useMemo(() => {
    if (restoreTargetMode === "TargetPath") {
      return targetPathDistinguishedName;
    }

    return detail?.lastKnownParent?.trim() ?? null;
  }, [detail?.lastKnownParent, restoreTargetMode, targetPathDistinguishedName]);

  const expectedRestoredDistinguishedName = useMemo(
    () => buildExpectedRestoredDistinguishedName(normalizedRestoreRdn, selectedRestoreParentDn),
    [normalizedRestoreRdn, selectedRestoreParentDn],
  );

  const ouSearchContext = detail
    ? resolveDeletedObjectOuSearchContext(detail.objectType)
    : "users";

  const isTargetPathReady =
    restoreTargetMode !== "TargetPath" || Boolean(targetPathDistinguishedName?.trim());

  const restoreMutation = useMutation({
    mutationFn: () => {
      if (restoreTargetMode === "TargetPath") {
        return restoreAdDeletedObject(id!, {
          restoreTargetMode: "TargetPath",
          targetPathDistinguishedName: targetPathDistinguishedName?.trim() ?? "",
        });
      }

      return restoreAdDeletedObject(id!);
    },
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:deletedObjects.errors.restoreFailed"));
        await invalidateAdManagementDeletedObjectRestoreQueries(queryClient);
        return;
      }

      await invalidateAdManagementDeletedObjectRestoreQueries(queryClient);
      toast.success(response.message || t("adManagement:deletedObjects.success.restore"));
      navigate(returnPath);
    },
    onError: async (error) => {
      toast.error(getApiErrorMessage(error, t("adManagement:deletedObjects.errors.restoreFailed")));
      await invalidateAdManagementDeletedObjectRestoreQueries(queryClient);
    },
  });

  if (!hasValidId) {
    return (
      <AdManagementModuleStateGuard>
        <section className="mx-auto w-full max-w-3xl space-y-4">
          <ErrorState
            title={t("adManagement:deletedObjects.errors.notFound")}
            description={t("adManagement:deletedObjects.errors.detailFailed")}
            retry={
              <Link to={returnPath} className={cn(buttonVariants({ variant: "outline", size: "sm" }))}>
                {t("common:actions.back")}
              </Link>
            }
          />
        </section>
      </AdManagementModuleStateGuard>
    );
  }

  const pageDescription = detail?.displayName?.trim() || detail?.name?.trim() || detail?.distinguishedName || undefined;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-3xl space-y-4">
        <PageHeader
          title={t("adManagement:deletedObjects.restore.pageTitle")}
          description={pageDescription}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        <p className="text-sm text-muted-foreground">
          {t("adManagement:deletedObjects.restore.pageDescription")}
        </p>

        {detailQuery.isLoading ? <LoadingState /> : null}

        {detailQuery.isError ? (
          <ErrorState
            title={
              detailQuery.error instanceof AxiosError && detailQuery.error.response?.status === 404
                ? t("adManagement:deletedObjects.errors.notFound")
                : t("adManagement:deletedObjects.errors.detailFailed")
            }
            description={getApiErrorMessage(
              detailQuery.error,
              t("adManagement:deletedObjects.errors.detailFailed"),
            )}
            retry={
              <Link to={returnPath} className={cn(buttonVariants({ variant: "outline", size: "sm" }))}>
                {t("common:actions.back")}
              </Link>
            }
          />
        ) : null}

        {detailQuery.isSuccess && detail && !isRestorable ? (
          <EmptyState
            title={t("adManagement:deletedObjects.restore.errors.notRestorable")}
            description={t("adManagement:deletedObjects.restore.errors.notRestorableDescription")}
          />
        ) : null}

        {detailQuery.isSuccess && detail && isRestorable ? (
          <form
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault();
              if (!isConfirmMatch || !isTargetPathReady || restoreMutation.isPending) {
                return;
              }

              restoreMutation.mutate();
            }}
          >
            <SectionCard title={t("adManagement:deletedObjects.restore.sections.deletedObject")}>
              <div className="grid gap-4 sm:grid-cols-2">
                <SummaryField
                  label={t("adManagement:deletedObjects.table.type")}
                  value={getAdDeletedObjectTypeLabel(t, detail.objectType)}
                />
                <SummaryField
                  label={t("adManagement:deletedObjects.fields.name")}
                  value={detail.name ?? detail.displayName}
                />
                <SummaryField
                  label={t("adManagement:deletedObjects.fields.samAccountName")}
                  value={detail.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:deletedObjects.fields.userPrincipalName")}
                  value={detail.userPrincipalName}
                />
                <div className="sm:col-span-2">
                  <SummaryField
                    label={t("adManagement:deletedObjects.fields.distinguishedName")}
                    value={detail.distinguishedName}
                    mono
                  />
                </div>
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:deletedObjects.restore.sections.restoreTarget")}>
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>{t("adManagement:deletedObjects.restore.targetMode.label")}</Label>
                  <div className="grid gap-2">
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="radio"
                        name="restore-target-mode"
                        value="OriginalLocation"
                        checked={restoreTargetMode === "OriginalLocation"}
                        onChange={() => {
                          setRestoreTargetMode("OriginalLocation");
                          setTargetPathDistinguishedName(null);
                        }}
                        disabled={restoreMutation.isPending}
                      />
                      <span>{t("adManagement:deletedObjects.restore.targetMode.originalLocation")}</span>
                    </label>
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="radio"
                        name="restore-target-mode"
                        value="TargetPath"
                        checked={restoreTargetMode === "TargetPath"}
                        onChange={() => setRestoreTargetMode("TargetPath")}
                        disabled={restoreMutation.isPending}
                      />
                      <span>{t("adManagement:deletedObjects.restore.targetMode.targetPath")}</span>
                    </label>
                  </div>
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="sm:col-span-2">
                    <SummaryField
                      label={t("adManagement:deletedObjects.restore.targetLocation")}
                      value={detail.lastKnownParent}
                      mono
                    />
                  </div>

                  {restoreTargetMode === "TargetPath" ? (
                    <div className="sm:col-span-2 space-y-2">
                      <AdOuSearchCombobox
                        value={targetPathDistinguishedName}
                        onChange={setTargetPathDistinguishedName}
                        disabled={restoreMutation.isPending}
                        searchContext={ouSearchContext}
                        fieldLabelKey="adManagement:deletedObjects.restore.targetPath.label"
                        placeholderKey="adManagement:deletedObjects.restore.targetPath.placeholder"
                        searchKey="common:actions.search"
                        emptyKey="common:select.noOptions"
                        errorKey="adManagement:deletedObjects.restore.targetPath.loadFailed"
                      />
                      <p className="text-xs text-muted-foreground">
                        {t("adManagement:deletedObjects.restore.targetPath.description")}
                      </p>
                    </div>
                  ) : null}

                  {restoreTargetMode === "TargetPath" && selectedRestoreParentDn ? (
                    <div className="sm:col-span-2">
                      <SummaryField
                        label={t("adManagement:deletedObjects.restore.targetPath.label")}
                        value={selectedRestoreParentDn}
                        mono
                      />
                    </div>
                  ) : null}

                  <SummaryField
                    label={t("adManagement:deletedObjects.restore.restoredRdn")}
                    value={normalizedRestoreRdn ?? detail.lastKnownRdn}
                    mono
                  />
                  <div className="sm:col-span-2">
                    <SummaryField
                      label={t("adManagement:deletedObjects.restore.expectedDistinguishedName")}
                      value={expectedRestoredDistinguishedName}
                      mono
                    />
                  </div>
                </div>
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:deletedObjects.restore.sections.technical")}>
              <div className="grid gap-4 sm:grid-cols-2">
                <SummaryField label={t("adManagement:deletedObjects.fields.objectGuid")} value={detail.id} mono />
                <SummaryField
                  label={t("adManagement:deletedObjects.fields.objectClass")}
                  value={detail.objectClass.join(", ")}
                />
                <SummaryField
                  label={t("adManagement:deletedObjects.fields.whenChanged")}
                  value={
                    detail.deletedAt ? (
                      <DateTimeText value={detail.deletedAt} />
                    ) : detail.whenChanged ? (
                      <DateTimeText value={detail.whenChanged} />
                    ) : (
                      "-"
                    )
                  }
                />
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:deletedObjects.restore.dialogTitle")}>
              <div className="space-y-2">
                <Label htmlFor="restore-deleted-object-confirm">
                  {confirmation?.usesSamAccountName
                    ? t("adManagement:deletedObjects.restore.confirmLabel")
                    : t("adManagement:deletedObjects.restore.confirmation.fallbackLabel")}
                </Label>
                <Input
                  id="restore-deleted-object-confirm"
                  value={confirmValue}
                  onChange={(event) => setConfirmValue(event.target.value)}
                  placeholder={expectedConfirmValue}
                  autoComplete="off"
                  disabled={restoreMutation.isPending}
                />
                <p className="text-xs text-muted-foreground">
                  {confirmation?.usesSamAccountName
                    ? t("adManagement:deletedObjects.restore.confirmation.samAccountNameHint", {
                        value: expectedConfirmValue,
                      })
                    : t("adManagement:deletedObjects.restore.confirmation.fallbackHint", {
                        value: expectedConfirmValue,
                      })}
                </p>
              </div>
            </SectionCard>

            <div className={AD_USER_FORM_ACTIONS_CLASSNAME}>
              <Link
                to={returnPath}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
              <Button
                type="submit"
                disabled={!isConfirmMatch || !isTargetPathReady || restoreMutation.isPending}
              >
                {t("adManagement:deletedObjects.restore.actions.submit")}
              </Button>
            </div>
          </form>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
