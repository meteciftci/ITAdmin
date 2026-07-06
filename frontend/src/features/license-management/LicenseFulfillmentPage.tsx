import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  convertLicenseRequestItems,
  getAllLicenseCompanies,
  getAllLicensePurchases,
  getFulfillmentCandidates,
  triageLicenseRequestItems,
} from "@/features/license-management/api";
import { FulfillmentPackageDefaultsForm } from "@/features/license-management/components/FulfillmentPackageDefaultsForm";
import {
  FulfillmentTargetForm,
  type FulfillmentTargetKind,
} from "@/features/license-management/components/FulfillmentTargetForm";
import {
  getRequestItemStatusLabel,
  getRequestSourceLabel,
  MANUAL_REQUEST_ITEM_STATUSES,
} from "@/features/license-management/enum-labels";
import {
  buildConvertPayload,
  clampFulfillQuantity,
  summarizeByProduct,
  validateSelection,
  type ConvertTarget,
  type FulfillmentSelectionLine,
} from "@/features/license-management/license-fulfillment-conversion";
import { LICENSE_REQUESTS_LIST_PATH } from "@/features/license-management/license-request-paths";
import type {
  ConvertFulfillmentNewPurchase,
  ConvertFulfillmentPackageDefaults,
  LicenseFulfillmentCandidate,
  LicenseRequestItemStatus,
} from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

function createDefaultNewPurchase(): ConvertFulfillmentNewPurchase {
  return {
    purchaseType: "DirectPurchase",
    title: "",
    description: null,
    purchaseDate: null,
    supplierCompanyId: null,
    supportCompanyId: null,
    actualTotalCost: null,
    currency: "TRY",
    vatIncluded: false,
    notes: null,
  };
}

function createDefaultPackageDefaults(productId: string): ConvertFulfillmentPackageDefaults {
  return {
    productId,
    licenseType: "Subscription",
    startDate: null,
    endDate: null,
    isPerpetual: false,
  };
}

export function LicenseFulfillmentPage() {
  const { t, i18n } = useTranslation(["licenseManagement", "common", "errors"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canFulfill = canAccess(user, PermissionCodes.LicenseManagement.FulfillRequests);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const [search, setSearch] = useState("");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [triageStatus, setTriageStatus] = useState<Record<string, LicenseRequestItemStatus>>({});
  const [triageApprovedQty, setTriageApprovedQty] = useState<Record<string, string>>({});
  const [targetKind, setTargetKind] = useState<FulfillmentTargetKind>("new");
  const [newPurchase, setNewPurchase] = useState<ConvertFulfillmentNewPurchase>(createDefaultNewPurchase);
  const [existingPurchaseId, setExistingPurchaseId] = useState("");
  const [packageDefaultsMap, setPackageDefaultsMap] = useState<
    Record<string, ConvertFulfillmentPackageDefaults>
  >({});

  const candidatesQuery = useQuery({
    queryKey: ["license-management", "fulfillment", "candidates"],
    queryFn: () => getFulfillmentCandidates({ pageNumber: 1, pageSize: 100 }),
    enabled: canFulfill,
  });

  const companiesQuery = useQuery({
    queryKey: ["license-management", "companies", "options"],
    queryFn: getAllLicenseCompanies,
    enabled: canFulfill,
  });

  const purchasesQuery = useQuery({
    queryKey: ["license-management", "purchases", "options"],
    queryFn: getAllLicensePurchases,
    enabled: canFulfill,
  });

  const candidates = useMemo(() => candidatesQuery.data?.items ?? [], [candidatesQuery.data]);

  const filteredCandidates = useMemo(() => {
    const term = search.trim().toLocaleLowerCase(dateLocale);
    if (!term) {
      return candidates;
    }

    return candidates.filter(
      (candidate) =>
        candidate.productName.toLocaleLowerCase(dateLocale).includes(term)
        || candidate.requesterUnitDisplayName.toLocaleLowerCase(dateLocale).includes(term),
    );
  }, [candidates, dateLocale, search]);

  const candidateById = useMemo(() => {
    const map = new Map<string, LicenseFulfillmentCandidate>();
    for (const candidate of candidates) {
      map.set(candidate.requestItemId, candidate);
    }
    return map;
  }, [candidates]);

  const selectionLines = useMemo<FulfillmentSelectionLine[]>(() => {
    const lines: FulfillmentSelectionLine[] = [];
    for (const id of selectedIds) {
      const candidate = candidateById.get(id);
      if (!candidate) {
        continue;
      }

      const fulfillQuantity = quantities[id] ?? candidate.remainingQuantity;
      lines.push({ candidate, fulfillQuantity });
    }
    return lines;
  }, [candidateById, quantities, selectedIds]);

  const productSummaries = useMemo(() => summarizeByProduct(selectionLines), [selectionLines]);

  const packageDefaultRows = useMemo(
    () =>
      productSummaries.map((summary) => ({
        ...(packageDefaultsMap[summary.productId] ?? createDefaultPackageDefaults(summary.productId)),
        productName: summary.productName,
      })),
    [packageDefaultsMap, productSummaries],
  );

  function toggleSelection(candidate: LicenseFulfillmentCandidate, checked: boolean) {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(candidate.requestItemId);
      } else {
        next.delete(candidate.requestItemId);
      }
      return next;
    });

    if (checked) {
      setQuantities((current) => ({
        ...current,
        [candidate.requestItemId]: current[candidate.requestItemId] ?? candidate.remainingQuantity,
      }));
    }
  }

  function updateQuantity(candidate: LicenseFulfillmentCandidate, rawValue: number) {
    setQuantities((current) => ({
      ...current,
      [candidate.requestItemId]: clampFulfillQuantity(rawValue, candidate.remainingQuantity),
    }));
  }

  function updatePackageDefaults(productId: string, patch: Partial<ConvertFulfillmentPackageDefaults>) {
    setPackageDefaultsMap((current) => ({
      ...current,
      [productId]: {
        ...(current[productId] ?? createDefaultPackageDefaults(productId)),
        ...patch,
      },
    }));
  }

  const triageMutation = useMutation({
    mutationFn: triageLicenseRequestItems,
    onSuccess: async () => {
      toast.success(t("licenseManagement:requests.fulfillment.messages.triaged"));
      setTriageStatus({});
      setTriageApprovedQty({});
      await queryClient.invalidateQueries({ queryKey: ["license-management", "fulfillment"] });
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("licenseManagement:requests.fulfillment.messages.operationFailed")),
      );
    },
  });

  const convertMutation = useMutation({
    mutationFn: convertLicenseRequestItems,
    onSuccess: () => {
      toast.success(t("licenseManagement:requests.fulfillment.messages.converted"));
      navigate(LICENSE_REQUESTS_LIST_PATH);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("licenseManagement:requests.fulfillment.messages.operationFailed")),
      );
    },
  });

  const triageEntries = useMemo(
    () => Object.entries(triageStatus).filter(([, status]) => Boolean(status)),
    [triageStatus],
  );

  function applyTriage() {
    if (triageEntries.length === 0) {
      return;
    }

    triageMutation.mutate(
      triageEntries.map(([requestItemId, status]) => {
        const approvedRaw = triageApprovedQty[requestItemId];
        const approvedQuantity =
          approvedRaw !== undefined && approvedRaw !== ""
            ? Number.parseInt(approvedRaw, 10)
            : null;

        return {
          requestItemId,
          status,
          approvedQuantity: Number.isFinite(approvedQuantity as number) ? approvedQuantity : null,
        };
      }),
    );
  }

  function handleConvert() {
    const validation = validateSelection(selectionLines);
    if (!validation.isValid) {
      toast.error(t(`licenseManagement:${validation.messageKey}`));
      return;
    }

    if (targetKind === "new" && !newPurchase.title.trim()) {
      toast.error(t("licenseManagement:requests.fulfillment.validation.titleRequired"));
      return;
    }

    if (targetKind === "existing" && !existingPurchaseId) {
      toast.error(t("licenseManagement:requests.fulfillment.validation.targetRequired"));
      return;
    }

    const target: ConvertTarget =
      targetKind === "new"
        ? { kind: "new", purchase: { ...newPurchase, title: newPurchase.title.trim() } }
        : { kind: "existing", purchaseId: existingPurchaseId };

    const packageDefaults: ConvertFulfillmentPackageDefaults[] = packageDefaultRows.map((row) => ({
      productId: row.productId,
      licenseType: row.licenseType,
      startDate: row.startDate,
      endDate: row.endDate,
      isPerpetual: row.isPerpetual,
    }));

    convertMutation.mutate(buildConvertPayload(selectionLines, target, packageDefaults));
  }

  if (!canFulfill) {
    return <Navigate to={LICENSE_REQUESTS_LIST_PATH} replace />;
  }

  const isBusy = convertMutation.isPending || triageMutation.isPending;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:requests.fulfillment.title")}
        description={t("licenseManagement:requests.fulfillment.description")}
        actions={
          <Link to={LICENSE_REQUESTS_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("licenseManagement:requests.fulfillment.backToRequests")}
          </Link>
        }
      />

      <SectionCard title={t("licenseManagement:requests.fulfillment.candidates.sectionTitle")}>
        <div className="space-y-4">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("licenseManagement:requests.fulfillment.candidates.searchPlaceholder")}
            className="max-w-sm"
          />

          {candidatesQuery.isLoading ? <LoadingState /> : null}
          {!candidatesQuery.isLoading && filteredCandidates.length === 0 ? (
            <EmptyState title={t("licenseManagement:requests.fulfillment.candidates.empty")} />
          ) : null}

          {filteredCandidates.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[960px] border-collapse text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.candidates.select")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.candidates.columns.requesterUnit")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.candidates.columns.product")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.candidates.columns.requestDate")}</th>
                    <th className="p-2 text-right">{t("licenseManagement:requests.fulfillment.candidates.columns.remaining")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.candidates.fulfillNow")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.triage.status")}</th>
                    <th className="p-2">{t("licenseManagement:requests.fulfillment.triage.approvedQuantity")}</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCandidates.map((candidate) => {
                    const isSelected = selectedIds.has(candidate.requestItemId);
                    return (
                      <tr key={candidate.requestItemId} className="border-b align-middle">
                        <td className="p-2">
                          <input
                            type="checkbox"
                            checked={isSelected}
                            disabled={isBusy}
                            aria-label={t("licenseManagement:requests.fulfillment.candidates.select")}
                            onChange={(event) => toggleSelection(candidate, event.target.checked)}
                          />
                        </td>
                        <td className="p-2">
                          <div className="space-y-0.5">
                            <p>{candidate.requesterUnitDisplayName}</p>
                            <p className="text-xs text-muted-foreground">
                              {getRequestSourceLabel(t, candidate.requestSource)}
                            </p>
                          </div>
                        </td>
                        <td className="p-2">
                          <div className="space-y-0.5">
                            <p>{candidate.productName}</p>
                            {candidate.productBrand ? (
                              <p className="text-xs text-muted-foreground">{candidate.productBrand}</p>
                            ) : null}
                          </div>
                        </td>
                        <td className="p-2">
                          <DateTimeText
                            value={candidate.requestDate}
                            options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                          />
                        </td>
                        <td className="p-2 text-right tabular-nums">{candidate.remainingQuantity}</td>
                        <td className="p-2">
                          <Input
                            type="number"
                            min="1"
                            max={candidate.remainingQuantity}
                            className="w-24"
                            disabled={!isSelected || isBusy}
                            value={quantities[candidate.requestItemId] ?? candidate.remainingQuantity}
                            onChange={(event) => updateQuantity(candidate, Number(event.target.value))}
                          />
                        </td>
                        <td className="p-2">
                          <Select
                            className="w-40"
                            disabled={isBusy}
                            value={triageStatus[candidate.requestItemId] ?? ""}
                            onChange={(event) =>
                              setTriageStatus((current) => {
                                const next = { ...current };
                                if (event.target.value) {
                                  next[candidate.requestItemId] = event.target.value as LicenseRequestItemStatus;
                                } else {
                                  delete next[candidate.requestItemId];
                                }
                                return next;
                              })
                            }
                          >
                            <option value="">{getRequestItemStatusLabel(t, candidate.itemStatus)}</option>
                            {MANUAL_REQUEST_ITEM_STATUSES.map((status) => (
                              <option key={status} value={status}>
                                {getRequestItemStatusLabel(t, status)}
                              </option>
                            ))}
                          </Select>
                        </td>
                        <td className="p-2">
                          <Input
                            type="number"
                            min="0"
                            className="w-24"
                            disabled={isBusy || !triageStatus[candidate.requestItemId]}
                            value={triageApprovedQty[candidate.requestItemId] ?? ""}
                            onChange={(event) =>
                              setTriageApprovedQty((current) => ({
                                ...current,
                                [candidate.requestItemId]: event.target.value,
                              }))
                            }
                          />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : null}

          <div className="flex justify-end">
            <Button
              type="button"
              variant="outline"
              disabled={isBusy || triageEntries.length === 0}
              onClick={applyTriage}
            >
              {t("licenseManagement:requests.fulfillment.triage.apply")}
            </Button>
          </div>
        </div>
      </SectionCard>

      {selectionLines.length > 0 ? (
        <>
          <SectionCard title={t("licenseManagement:requests.fulfillment.conversion.target")}>
            <FulfillmentTargetForm
              targetKind={targetKind}
              onTargetKindChange={setTargetKind}
              newPurchase={newPurchase}
              onNewPurchaseChange={setNewPurchase}
              existingPurchaseId={existingPurchaseId}
              onExistingPurchaseChange={setExistingPurchaseId}
              companies={companiesQuery.data ?? []}
              purchases={purchasesQuery.data ?? []}
              dateLocale={dateLocale}
              disabled={isBusy}
            />
          </SectionCard>

          <SectionCard title={t("licenseManagement:requests.fulfillment.packageDefaults.sectionTitle")}>
            <FulfillmentPackageDefaultsForm
              defaults={packageDefaultRows}
              onChange={updatePackageDefaults}
              dateLocale={dateLocale}
              disabled={isBusy}
            />
          </SectionCard>

          <SectionCard title={t("licenseManagement:requests.fulfillment.conversion.summaryTitle")}>
            <div className="space-y-4">
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="p-2">{t("licenseManagement:requests.fulfillment.conversion.summaryProduct")}</th>
                      <th className="p-2 text-right">{t("licenseManagement:requests.fulfillment.conversion.summaryLineCount")}</th>
                      <th className="p-2 text-right">{t("licenseManagement:requests.fulfillment.conversion.summaryQuantity")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productSummaries.map((summary) => (
                      <tr key={summary.productId} className="border-b">
                        <td className="p-2">{summary.productName}</td>
                        <td className="p-2 text-right tabular-nums">{summary.lineCount}</td>
                        <td className="p-2 text-right tabular-nums">{summary.totalQuantity}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="flex justify-end">
                <Button type="button" disabled={isBusy} onClick={handleConvert}>
                  {convertMutation.isPending
                    ? t("licenseManagement:requests.fulfillment.conversion.converting")
                    : t("licenseManagement:requests.fulfillment.conversion.convert")}
                </Button>
              </div>
            </div>
          </SectionCard>
        </>
      ) : null}
    </section>
  );
}
