import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
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
import { Select } from "@/components/ui/select";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  convertLicenseRequestItems,
  getAllFulfillmentCandidates,
  getAllLicenseCompanies,
  getAllLicensePackages,
  getAllLicensedProducts,
  getAllLicensePurchases,
  triageLicenseRequestItems,
} from "@/features/license-management/api";
import { FulfillmentPackageDefaultsForm } from "@/features/license-management/components/FulfillmentPackageDefaultsForm";
import {
  FulfillmentTargetForm,
  type FulfillmentTargetKind,
} from "@/features/license-management/components/FulfillmentTargetForm";
import { ManualLinesSection } from "@/features/license-management/components/ManualLinesSection";
import type { ManualLineDraft } from "@/features/license-management/components/manual-line-draft";
import { RenewalLinesSection } from "@/features/license-management/components/RenewalLinesSection";
import {
  getLicenseTypeLabel,
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
  ConvertFulfillmentRenewalLine,
  LicenseFulfillmentCandidate,
  LicenseRequestItemStatus,
} from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { validatePackageDateFields } from "@/features/license-management/form-validation";
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

function createDefaultPackageDefaults(
  productId: string,
  licenseType: ConvertFulfillmentPackageDefaults["licenseType"],
): ConvertFulfillmentPackageDefaults {
  return {
    productId,
    licenseType,
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
  const [fulfillmentUserIds, setFulfillmentUserIds] = useState<Record<string, string[]>>({});
  const [triageStatus, setTriageStatus] = useState<Record<string, LicenseRequestItemStatus>>({});
  const [triageApprovedQty, setTriageApprovedQty] = useState<Record<string, string>>({});
  const [triageUserIds, setTriageUserIds] = useState<Record<string, string[]>>({});
  const [targetKind, setTargetKind] = useState<FulfillmentTargetKind>("new");
  const [newPurchase, setNewPurchase] = useState<ConvertFulfillmentNewPurchase>(createDefaultNewPurchase);
  const [existingPurchaseId, setExistingPurchaseId] = useState("");
  const [packageDefaultsMap, setPackageDefaultsMap] = useState<
    Record<string, ConvertFulfillmentPackageDefaults>
  >({});
  const [renewalRows, setRenewalRows] = useState<ConvertFulfillmentRenewalLine[]>([]);
  const [manualRows, setManualRows] = useState<ManualLineDraft[]>([]);

  const candidatesQuery = useQuery({
    queryKey: ["license-management", "fulfillment", "candidates"],
    queryFn: () => getAllFulfillmentCandidates(),
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

  const renewablePackagesQuery = useQuery({
    queryKey: ["license-management", "packages", "renewable"],
    queryFn: () => getAllLicensePackages(),
    enabled: canFulfill,
  });

  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "all"],
    queryFn: getAllLicensedProducts,
    enabled: canFulfill,
  });

  const candidates = useMemo(() => candidatesQuery.data ?? [], [candidatesQuery.data]);

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

      const requestItemUserIds = fulfillmentUserIds[id];
      const fulfillQuantity = candidate.licenseType === "NamedUser"
        ? requestItemUserIds?.length ?? 0
        : quantities[id] ?? candidate.remainingQuantity;
      lines.push({ candidate, fulfillQuantity, requestItemUserIds });
    }
    return lines;
  }, [candidateById, fulfillmentUserIds, quantities, selectedIds]);

  const productSummaries = useMemo(() => summarizeByProduct(selectionLines), [selectionLines]);

  const packageDefaultRows = useMemo(
    () =>
      productSummaries.map((summary) => ({
        ...(packageDefaultsMap[summary.groupKey]
          ?? createDefaultPackageDefaults(summary.productId, summary.licenseType)),
        groupKey: summary.groupKey,
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
      if (candidate.licenseType === "NamedUser") {
        setFulfillmentUserIds((current) => ({
          ...current,
          [candidate.requestItemId]: current[candidate.requestItemId]
            ?? candidate.users
              .filter((user) => user.status === "Approved")
              .slice(0, candidate.remainingQuantity)
              .map((user) => user.id),
        }));
      }
    }
  }

  function toggleFulfillmentUser(candidate: LicenseFulfillmentCandidate, userId: string, checked: boolean) {
    setFulfillmentUserIds((current) => {
      const selected = new Set(current[candidate.requestItemId] ?? []);
      if (checked) {
        if (selected.size < candidate.remainingQuantity) {
          selected.add(userId);
        }
      } else {
        selected.delete(userId);
      }
      return { ...current, [candidate.requestItemId]: [...selected] };
    });
  }

  function toggleTriageUser(candidate: LicenseFulfillmentCandidate, userId: string, checked: boolean) {
    const selected = new Set(triageUserIds[candidate.requestItemId] ?? []);
    if (checked) {
      selected.add(userId);
    } else {
      selected.delete(userId);
    }
    const ids = [...selected];
    setTriageUserIds((current) => ({ ...current, [candidate.requestItemId]: ids }));
    setTriageApprovedQty((current) => ({
      ...current,
      [candidate.requestItemId]: String(ids.length),
    }));
  }

  function updateQuantity(candidate: LicenseFulfillmentCandidate, rawValue: number) {
    setQuantities((current) => ({
      ...current,
      [candidate.requestItemId]: clampFulfillQuantity(rawValue, candidate.remainingQuantity),
    }));
  }

  function updatePackageDefaults(
    groupKey: string,
    productId: string,
    licenseType: ConvertFulfillmentPackageDefaults["licenseType"],
    patch: Partial<ConvertFulfillmentPackageDefaults>,
  ) {
    setPackageDefaultsMap((current) => ({
      ...current,
      [groupKey]: {
        ...(current[groupKey] ?? createDefaultPackageDefaults(productId, licenseType)),
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
      setTriageUserIds({});
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
          ...(candidateById.get(requestItemId)?.licenseType === "NamedUser"
            && status === "Approved"
            ? { approvedUserIds: triageUserIds[requestItemId] ?? [] }
            : {}),
        };
      }),
    );
  }

  const manualLines = useMemo(
    () =>
      manualRows.map((row) => ({
        productId: row.productId,
        quantity: row.quantity,
        licenseType: row.licenseType,
        startDate: row.startDate,
        endDate: row.endDate,
        isPerpetual: row.isPerpetual,
      })),
    [manualRows],
  );

  const hasAnyLine =
    selectionLines.length > 0 || renewalRows.length > 0 || manualRows.length > 0;

  function handleConvert() {
    if (!hasAnyLine) {
      toast.error(t("licenseManagement:requests.fulfillment.validation.noLines"));
      return;
    }

    if (selectionLines.length > 0) {
      const validation = validateSelection(selectionLines);
      if (!validation.isValid) {
        toast.error(t(`licenseManagement:${validation.messageKey}`));
        return;
      }
    }

    if (manualRows.some((row) => !row.productId)) {
      toast.error(t("licenseManagement:requests.fulfillment.manual.productRequired"));
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

    const invalidPackageDates = [
      ...packageDefaultRows.map((row) => validatePackageDateFields(
        row.startDate, row.endDate, row.isPerpetual, false, null,
      )),
      ...renewalRows.map((row) => validatePackageDateFields(
        row.startDate,
        row.endDate,
        row.isPerpetual,
        row.renewalRequired,
        row.renewalDate,
      )),
      ...manualRows.map((row) => validatePackageDateFields(
        row.startDate, row.endDate, row.isPerpetual, false, null,
      )),
    ].find((key) => key !== null);
    if (invalidPackageDates) {
      toast.error(t(`licenseManagement:messages.${invalidPackageDates}`));
      return;
    }

    if (renewalRows.some((row) => {
      const source = renewablePackagesQuery.data?.find(
        (pkg) => pkg.id === row.sourcePackageId,
      );
      return source?.status === "Active" || source?.status === "Suspended"
        ? !row.expireSourcePackage
        : false;
    })) {
      toast.error(t("licenseManagement:requests.fulfillment.validation.expireSourceRequired"));
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

    convertMutation.mutate(
      buildConvertPayload(selectionLines, target, packageDefaults, renewalRows, manualLines),
    );
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
          {candidatesQuery.isError ? (
            <ErrorState
              title={t("errors:generic.title")}
              description={getApiErrorMessage(candidatesQuery.error, t("errors:generic.description"))}
            />
          ) : null}
          {!candidatesQuery.isLoading && !candidatesQuery.isError && filteredCandidates.length === 0 ? (
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
                            disabled={isBusy || !candidate.isFulfillable}
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
                            <p className="text-xs text-muted-foreground">
                              {getLicenseTypeLabel(t, candidate.licenseType)}
                            </p>
                            {candidate.productBrand ? (
                              <p className="text-xs text-muted-foreground">{candidate.productBrand}</p>
                            ) : null}
                            {candidate.licenseType === "NamedUser" && isSelected ? (
                              <div className="mt-2 space-y-1 rounded border p-2">
                                <p className="text-xs font-medium">
                                  {t("licenseManagement:requests.fulfillment.candidates.fulfillUsers")}
                                </p>
                                {candidate.users.filter((user) => user.status === "Approved").map((user) => (
                                  <label key={user.id} className="flex items-center gap-2 text-xs">
                                    <input
                                      type="checkbox"
                                      disabled={isBusy}
                                      checked={(fulfillmentUserIds[candidate.requestItemId] ?? []).includes(user.id)}
                                      onChange={(event) =>
                                        toggleFulfillmentUser(candidate, user.id, event.target.checked)}
                                    />
                                    <span>{user.displayName ?? user.userPrincipalName ?? user.adObjectId}</span>
                                  </label>
                                ))}
                              </div>
                            ) : null}
                            {candidate.licenseType === "NamedUser"
                              && triageStatus[candidate.requestItemId] === "Approved" ? (
                                <div className="mt-2 space-y-1 rounded border p-2">
                                  <p className="text-xs font-medium">
                                    {t("licenseManagement:requests.fulfillment.candidates.approveUsers")}
                                  </p>
                                  {candidate.users.map((user) => (
                                    <label key={user.id} className="flex items-center gap-2 text-xs">
                                      <input
                                        type="checkbox"
                                        disabled={isBusy}
                                        checked={(triageUserIds[candidate.requestItemId] ?? []).includes(user.id)}
                                        onChange={(event) =>
                                          toggleTriageUser(candidate, user.id, event.target.checked)}
                                      />
                                      <span>{user.displayName ?? user.userPrincipalName ?? user.adObjectId}</span>
                                    </label>
                                  ))}
                                </div>
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
                            disabled={!isSelected || isBusy || candidate.licenseType === "NamedUser"}
                            value={candidate.licenseType === "NamedUser"
                              ? fulfillmentUserIds[candidate.requestItemId]?.length ?? 0
                              : quantities[candidate.requestItemId] ?? candidate.remainingQuantity}
                            onChange={(event) => updateQuantity(candidate, Number(event.target.value))}
                          />
                        </td>
                        <td className="p-2">
                          <Select
                            className="w-40"
                            disabled={isBusy}
                            value={triageStatus[candidate.requestItemId] ?? ""}
                            onChange={(event) => {
                              const status = event.target.value as LicenseRequestItemStatus | "";
                              setTriageStatus((current) => {
                                const next = { ...current };
                                if (status) {
                                  next[candidate.requestItemId] = status;
                                } else {
                                  delete next[candidate.requestItemId];
                                }
                                return next;
                              });
                              if (status === "Approved") {
                                const defaultUsers = candidate.licenseType === "NamedUser"
                                  ? candidate.users.filter((user) => user.status !== "Fulfilled").map((user) => user.id)
                                  : [];
                                setTriageUserIds((current) => ({
                                  ...current,
                                  [candidate.requestItemId]: defaultUsers,
                                }));
                                setTriageApprovedQty((current) => ({
                                  ...current,
                                  [candidate.requestItemId]: String(
                                    candidate.licenseType === "NamedUser"
                                      ? defaultUsers.length
                                      : candidate.approvedQuantity ?? candidate.requestedQuantity,
                                  ),
                                }));
                              }
                            }}
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
                            disabled={isBusy
                              || triageStatus[candidate.requestItemId] !== "Approved"
                              || candidate.licenseType === "NamedUser"}
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

      <SectionCard title={t("licenseManagement:requests.fulfillment.renewal.sectionTitle")}>
        <RenewalLinesSection
          rows={renewalRows}
          onChange={setRenewalRows}
          packages={renewablePackagesQuery.data ?? []}
          dateLocale={dateLocale}
          disabled={isBusy}
        />
      </SectionCard>

      <SectionCard title={t("licenseManagement:requests.fulfillment.manual.sectionTitle")}>
        <ManualLinesSection
          rows={manualRows}
          onChange={setManualRows}
          products={productsQuery.data ?? []}
          dateLocale={dateLocale}
          disabled={isBusy}
        />
      </SectionCard>

      {hasAnyLine ? (
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
              purchases={(purchasesQuery.data ?? []).filter(
                (purchase) => purchase.status === "Draft" || purchase.status === "Active",
              )}
              dateLocale={dateLocale}
              disabled={isBusy}
            />
          </SectionCard>

          {packageDefaultRows.length > 0 ? (
            <SectionCard title={t("licenseManagement:requests.fulfillment.packageDefaults.sectionTitle")}>
              <FulfillmentPackageDefaultsForm
                defaults={packageDefaultRows}
                onChange={updatePackageDefaults}
                dateLocale={dateLocale}
                disabled={isBusy}
              />
            </SectionCard>
          ) : null}

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
                      <tr key={summary.groupKey} className="border-b">
                        <td className="p-2">
                          {summary.productName} · {getLicenseTypeLabel(t, summary.licenseType)}
                        </td>
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
