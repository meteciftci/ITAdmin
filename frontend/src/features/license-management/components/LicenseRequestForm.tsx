import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  createLicenseRequest,
  getAllLicensedProducts,
  getLicenseManagementSettings,
  LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY,
  updateLicenseRequest,
} from "@/features/license-management/api";
import { LicenseOuPicker } from "@/features/license-management/components/LicenseOuPicker";
import { LicenseRequestItemsEditor } from "@/features/license-management/components/LicenseRequestItemsEditor";
import {
  getRequestSourceLabel,
  getRequestStatusLabel,
  MANUAL_REQUEST_STATUSES,
  REQUEST_SOURCES,
} from "@/features/license-management/enum-labels";
import { validateLicenseRequestForm } from "@/features/license-management/license-request-form-validation";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";
import {
  buildLicenseRequestPayload,
  calculateItemsEstimatedTotal,
  createEmptyRequestItemDraft,
  mapDetailToItemDrafts,
  type LicenseRequestItemDraft,
} from "@/features/license-management/license-request-payload";
import { isRequestSourceFieldVisible } from "@/features/license-management/request-source-fields";
import type {
  LicenseRequestDetail,
  LicenseRequestOuSnapshot,
  LicenseRequestSource,
  LicenseRequestStatus,
} from "@/features/license-management/types";

type Props = {
  mode: "create" | "edit";
  request?: LicenseRequestDetail | null;
  onCancel: () => void;
  onSaved: (requestId: string) => void;
};

function SectionTitle({ children }: { children: string }) {
  return <h3 className="text-sm font-semibold text-foreground">{children}</h3>;
}

function toDateOnly(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  return value.slice(0, 10);
}

export function LicenseRequestForm({ mode, request, onCancel, onSaved }: Props) {
  const { t, i18n } = useTranslation(["licenseManagement", "common"]);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const [requestSource, setRequestSource] = useState<LicenseRequestSource>("OfficialLetter");
  const [requestDate, setRequestDate] = useState<string | null>(null);
  const [externalRequestNumber, setExternalRequestNumber] = useState("");
  const [ebysNumber, setEbysNumber] = useState("");
  const [ebysDate, setEbysDate] = useState<string | null>(null);
  const [requesterUnit, setRequesterUnit] = useState<LicenseRequestOuSnapshot | null>(null);
  const [requesterManagerName, setRequesterManagerName] = useState("");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState<LicenseRequestStatus>("Pending");
  const [estimatedTotalCost, setEstimatedTotalCost] = useState("");
  const [currency, setCurrency] = useState("TRY");
  const [vatIncluded, setVatIncluded] = useState(false);
  const [costNote, setCostNote] = useState("");
  const [items, setItems] = useState<LicenseRequestItemDraft[]>([createEmptyRequestItemDraft()]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const settingsQuery = useQuery({
    queryKey: LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getLicenseManagementSettings,
  });

  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "options"],
    queryFn: getAllLicensedProducts,
  });

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setRequestSource(request?.requestSource ?? "OfficialLetter");
    setRequestDate(toDateOnly(request?.requestDate));
    setExternalRequestNumber(request?.externalRequestNumber ?? "");
    setEbysNumber(request?.ebysNumber ?? "");
    setEbysDate(toDateOnly(request?.ebysDate));
    setRequesterUnit(
      request
        ? {
            objectGuid: request.requesterUnitObjectGuid,
            displayName: request.requesterUnitDisplayName,
            distinguishedName: request.requesterUnitDistinguishedName,
          }
        : null,
    );
    setRequesterManagerName(request?.requesterManagerName ?? "");
    setDescription(request?.description ?? "");
    setStatus(request?.status ?? "Pending");
    setEstimatedTotalCost(request?.estimatedTotalCost?.toString() ?? "");
    setCurrency(request?.currency ?? settingsQuery.data?.defaultCurrency ?? "TRY");
    setVatIncluded(request?.vatIncluded ?? settingsQuery.data?.defaultVatIncluded ?? false);
    setCostNote(request?.costNote ?? "");
    setItems(request ? mapDetailToItemDrafts(request) : [createEmptyRequestItemDraft(currency)]);
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [currency, request, settingsQuery.data?.defaultCurrency, settingsQuery.data?.defaultVatIncluded]);

  const computedTotal = useMemo(() => calculateItemsEstimatedTotal(items), [items]);
  const showExternalRequestNumber = isRequestSourceFieldVisible("externalRequestNumber", requestSource);
  const showEbysFields = isRequestSourceFieldVisible("ebysNumber", requestSource);
  const showDescriptionInRequestInfo = isRequestSourceFieldVisible("description", requestSource);

  const saveMutation = useMutation({
    mutationFn: async () => {
      const validation = validateLicenseRequestForm(t, {
        requestDate,
        requestSource,
        requesterUnit,
        externalRequestNumber,
        ebysNumber,
        ebysDate,
        items,
      });

      if (!validation.isValid) {
        throw new Error(validation.message);
      }

      if (!requesterUnit) {
        throw new Error(t("licenseManagement:requests.validation.requesterUnitRequired"));
      }

      const payload = buildLicenseRequestPayload({
        requestSource,
        requestDate: requestDate!,
        externalRequestNumber,
        ebysNumber,
        ebysDate,
        requesterUnit,
        requesterManagerName,
        description,
        status,
        estimatedTotalCost,
        currency,
        vatIncluded,
        costNote,
        items,
      });

      if (mode === "create") {
        return createLicenseRequest(payload);
      }

      return updateLicenseRequest(request!.id, payload);
    },
    onSuccess: (savedRequest) => {
      onSaved(savedRequest.id);
    },
    onError: (error) => {
      setErrorMessage(
        getLicenseManagementApiErrorMessage(error, t, "common:messages.operationFailed"),
      );
    },
  });

  return (
    <div className="space-y-6">
      {errorMessage ? <FormError message={errorMessage} /> : null}

      <section className="space-y-4">
        <SectionTitle>{t("licenseManagement:requests.sections.requestInfo")}</SectionTitle>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="request-source">{t("licenseManagement:requests.fields.requestSource")}</Label>
            <Select
              id="request-source"
              value={requestSource}
              onChange={(event) => setRequestSource(event.target.value as LicenseRequestSource)}
            >
              {REQUEST_SOURCES.map((source) => (
                <option key={source} value={source}>
                  {getRequestSourceLabel(t, source)}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-2">
            <Label>{t("licenseManagement:requests.fields.requestDate")}</Label>
            <DatePicker
              value={requestDate}
              onChange={setRequestDate}
              placeholder={t("licenseManagement:requests.fields.requestDate")}
              clearLabel={t("common:actions.clear")}
              locale={dateLocale}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="request-status">{t("common:fields.status")}</Label>
            <Select
              id="request-status"
              value={status}
              onChange={(event) => setStatus(event.target.value as LicenseRequestStatus)}
            >
              {MANUAL_REQUEST_STATUSES.map((itemStatus) => (
                <option key={itemStatus} value={itemStatus}>
                  {getRequestStatusLabel(t, itemStatus)}
                </option>
              ))}
            </Select>
          </div>
          {showExternalRequestNumber ? (
            <div className="space-y-2">
              <Label htmlFor="external-request-number">
                {t("licenseManagement:requests.fields.externalRequestNumber")}
              </Label>
              <Input
                id="external-request-number"
                value={externalRequestNumber}
                onChange={(event) => setExternalRequestNumber(event.target.value)}
              />
            </div>
          ) : null}
          {showEbysFields ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="ebys-number">{t("licenseManagement:requests.fields.ebysNumber")}</Label>
                <Input
                  id="ebys-number"
                  value={ebysNumber}
                  onChange={(event) => setEbysNumber(event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>{t("licenseManagement:requests.fields.ebysDate")}</Label>
                <DatePicker
                  value={ebysDate}
                  onChange={setEbysDate}
                  placeholder={t("licenseManagement:requests.fields.ebysDate")}
                  clearLabel={t("common:actions.clear")}
                  locale={dateLocale}
                />
              </div>
            </>
          ) : null}
          {showDescriptionInRequestInfo ? (
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="request-description">{t("common:fields.description")}</Label>
              <Textarea
                id="request-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
          ) : null}
        </div>
      </section>

      <section className="space-y-4">
        <SectionTitle>{t("licenseManagement:requests.sections.unitAndManager")}</SectionTitle>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <LicenseOuPicker
              value={requesterUnit}
              onChange={setRequesterUnit}
              label={t("licenseManagement:requests.fields.requesterUnit")}
              placeholder={t("licenseManagement:requests.placeholders.selectUnit")}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="requester-manager-name">
              {t("licenseManagement:requests.fields.requesterManagerName")}
            </Label>
            <Input
              id="requester-manager-name"
              value={requesterManagerName}
              onChange={(event) => setRequesterManagerName(event.target.value)}
            />
          </div>
        </div>
      </section>

      <section className="space-y-4">
        <SectionTitle>{t("licenseManagement:requests.sections.items")}</SectionTitle>
        <LicenseRequestItemsEditor
          items={items}
          products={productsQuery.data ?? []}
          defaultCurrency={currency}
          onChange={setItems}
        />
      </section>

      <section className="space-y-4">
        <SectionTitle>{t("licenseManagement:requests.sections.cost")}</SectionTitle>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="estimated-total-cost">
              {t("licenseManagement:requests.fields.estimatedTotalCost")}
            </Label>
            <Input
              id="estimated-total-cost"
              type="number"
              min="0"
              step="0.01"
              value={estimatedTotalCost}
              placeholder={computedTotal > 0 ? computedTotal.toString() : undefined}
              onChange={(event) => setEstimatedTotalCost(event.target.value)}
            />
            {computedTotal > 0 ? (
              <p className="text-xs text-muted-foreground">
                {t("licenseManagement:requests.fields.calculatedTotal", { value: computedTotal })}
              </p>
            ) : null}
          </div>
          <div className="space-y-2">
            <Label htmlFor="request-currency">{t("licenseManagement:requests.fields.currency")}</Label>
            <Input
              id="request-currency"
              value={currency}
              onChange={(event) => setCurrency(event.target.value)}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <CheckboxField
              id="request-vat-included"
              label={t("licenseManagement:requests.fields.vatIncluded")}
              checked={vatIncluded}
              onCheckedChange={setVatIncluded}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="cost-note">{t("licenseManagement:requests.fields.costNote")}</Label>
            <Input
              id="cost-note"
              value={costNote}
              onChange={(event) => setCostNote(event.target.value)}
            />
          </div>
        </div>
      </section>

      <div className="flex flex-wrap items-center justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={saveMutation.isPending}>
          {t("common:actions.cancel")}
        </Button>
        <Button type="button" onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
          {t("common:actions.save")}
        </Button>
      </div>
    </div>
  );
}
