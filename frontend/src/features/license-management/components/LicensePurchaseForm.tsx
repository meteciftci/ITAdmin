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
  createLicensePurchase,
  getAllLicenseCompanies,
  updateLicensePurchase,
} from "@/features/license-management/api";
import {
  getPurchaseStatusLabel,
  getPurchaseTypeLabel,
  PURCHASE_STATUSES,
  PURCHASE_TYPES,
} from "@/features/license-management/enum-labels";
import { validatePurchaseForm } from "@/features/license-management/form-validation";
import type {
  LicensePurchaseDetail,
  LicensePurchaseStatus,
  LicensePurchaseType,
} from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  mode: "create" | "edit";
  purchase?: LicensePurchaseDetail | null;
  onCancel: () => void;
  onSaved: () => void;
};

function toDateOnly(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  return value.slice(0, 10);
}

export function LicensePurchaseForm({ mode, purchase, onCancel, onSaved }: Props) {
  const { t, i18n } = useTranslation(["licenseManagement", "common"]);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const [purchaseType, setPurchaseType] = useState<LicensePurchaseType>("DirectPurchase");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [purchaseDate, setPurchaseDate] = useState<string | null>(null);
  const [tenderNumber, setTenderNumber] = useState("");
  const [tenderDate, setTenderDate] = useState<string | null>(null);
  const [directPurchaseNumber, setDirectPurchaseNumber] = useState("");
  const [dmoOrderNumber, setDmoOrderNumber] = useState("");
  const [ebysNumber, setEbysNumber] = useState("");
  const [ebysDate, setEbysDate] = useState<string | null>(null);
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState<string | null>(null);
  const [contractNumber, setContractNumber] = useState("");
  const [contractStartDate, setContractStartDate] = useState<string | null>(null);
  const [contractEndDate, setContractEndDate] = useState<string | null>(null);
  const [supplierCompanyId, setSupplierCompanyId] = useState("");
  const [supportCompanyId, setSupportCompanyId] = useState("");
  const [actualTotalCost, setActualTotalCost] = useState("");
  const [currency, setCurrency] = useState("");
  const [vatIncluded, setVatIncluded] = useState(false);
  const [notes, setNotes] = useState("");
  const [status, setStatus] = useState<LicensePurchaseStatus>("Draft");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const companiesQuery = useQuery({
    queryKey: ["license-management", "companies", "options"],
    queryFn: getAllLicenseCompanies,
  });

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setPurchaseType(purchase?.purchaseType ?? "DirectPurchase");
    setTitle(purchase?.title ?? "");
    setDescription(purchase?.description ?? "");
    setPurchaseDate(toDateOnly(purchase?.purchaseDate));
    setTenderNumber(purchase?.tenderNumber ?? "");
    setTenderDate(toDateOnly(purchase?.tenderDate));
    setDirectPurchaseNumber(purchase?.directPurchaseNumber ?? "");
    setDmoOrderNumber(purchase?.dmoOrderNumber ?? "");
    setEbysNumber(purchase?.ebysNumber ?? "");
    setEbysDate(toDateOnly(purchase?.ebysDate));
    setInvoiceNumber(purchase?.invoiceNumber ?? "");
    setInvoiceDate(toDateOnly(purchase?.invoiceDate));
    setContractNumber(purchase?.contractNumber ?? "");
    setContractStartDate(toDateOnly(purchase?.contractStartDate));
    setContractEndDate(toDateOnly(purchase?.contractEndDate));
    setSupplierCompanyId(purchase?.supplierCompanyId ?? "");
    setSupportCompanyId(purchase?.supportCompanyId ?? "");
    setActualTotalCost(purchase?.actualTotalCost?.toString() ?? "");
    setCurrency(purchase?.currency ?? "");
    setVatIncluded(purchase?.vatIncluded ?? false);
    setNotes(purchase?.notes ?? "");
    setStatus(purchase?.status ?? "Draft");
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [purchase]);

  const validationKey = useMemo(() => validatePurchaseForm(title), [title]);

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        purchaseType,
        title: title.trim(),
        description: description || null,
        purchaseDate,
        tenderNumber: tenderNumber || null,
        tenderDate,
        directPurchaseNumber: directPurchaseNumber || null,
        dmoOrderNumber: dmoOrderNumber || null,
        ebysNumber: ebysNumber || null,
        ebysDate,
        invoiceNumber: invoiceNumber || null,
        invoiceDate,
        contractNumber: contractNumber || null,
        contractStartDate,
        contractEndDate,
        supplierCompanyId: supplierCompanyId || null,
        supportCompanyId: supportCompanyId || null,
        actualTotalCost: actualTotalCost ? Number(actualTotalCost) : null,
        currency: currency || null,
        vatIncluded,
        notes: notes || null,
        status,
      };
      if (mode === "edit" && purchase) {
        await updateLicensePurchase(purchase.id, payload);
      } else {
        await createLicensePurchase(payload);
      }
    },
    onSuccess: () => {
      onSaved();
    },
    onError: (error) => {
      setErrorMessage(getApiErrorMessage(error, t("licenseManagement:messages.operationFailed")));
    },
  });

  return (
    <div className="space-y-4">
      {errorMessage ? <FormError message={errorMessage} /> : null}
      {validationKey ? <FormError message={t(`licenseManagement:messages.${validationKey}`)} /> : null}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.purchaseType")}</Label>
          <Select value={purchaseType} onChange={(e) => setPurchaseType(e.target.value as LicensePurchaseType)}>
            {PURCHASE_TYPES.map((type) => (
              <option key={type} value={type}>{getPurchaseTypeLabel(t, type)}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.status")}</Label>
          <Select value={status} onChange={(e) => setStatus(e.target.value as LicensePurchaseStatus)}>
            {PURCHASE_STATUSES.map((item) => (
              <option key={item} value={item}>{getPurchaseStatusLabel(t, item)}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>{t("licenseManagement:form.title")}</Label>
          <Input value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>{t("licenseManagement:form.description")}</Label>
          <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.purchaseDate")}</Label>
          <DatePicker
            value={purchaseDate}
            onChange={setPurchaseDate}
            placeholder={t("licenseManagement:form.purchaseDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.contractNumber")}</Label>
          <Input value={contractNumber} onChange={(e) => setContractNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.contractStartDate")}</Label>
          <DatePicker
            value={contractStartDate}
            onChange={setContractStartDate}
            placeholder={t("licenseManagement:form.contractStartDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.contractEndDate")}</Label>
          <DatePicker
            value={contractEndDate}
            onChange={setContractEndDate}
            placeholder={t("licenseManagement:form.contractEndDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.tenderNumber")}</Label>
          <Input value={tenderNumber} onChange={(e) => setTenderNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.tenderDate")}</Label>
          <DatePicker
            value={tenderDate}
            onChange={setTenderDate}
            placeholder={t("licenseManagement:form.tenderDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.directPurchaseNumber")}</Label>
          <Input value={directPurchaseNumber} onChange={(e) => setDirectPurchaseNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.dmoOrderNumber")}</Label>
          <Input value={dmoOrderNumber} onChange={(e) => setDmoOrderNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.ebysNumber")}</Label>
          <Input value={ebysNumber} onChange={(e) => setEbysNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.ebysDate")}</Label>
          <DatePicker
            value={ebysDate}
            onChange={setEbysDate}
            placeholder={t("licenseManagement:form.ebysDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.invoiceNumber")}</Label>
          <Input value={invoiceNumber} onChange={(e) => setInvoiceNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.invoiceDate")}</Label>
          <DatePicker
            value={invoiceDate}
            onChange={setInvoiceDate}
            placeholder={t("licenseManagement:form.invoiceDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.supplierCompany")}</Label>
          <Select value={supplierCompanyId} onChange={(e) => setSupplierCompanyId(e.target.value)}>
            <option value="">{t("licenseManagement:form.selectCompany")}</option>
            {(companiesQuery.data ?? []).map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.supportCompany")}</Label>
          <Select value={supportCompanyId} onChange={(e) => setSupportCompanyId(e.target.value)}>
            <option value="">{t("licenseManagement:form.selectCompany")}</option>
            {(companiesQuery.data ?? []).map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.actualTotalCost")}</Label>
          <Input type="number" min="0" step="0.01" value={actualTotalCost} onChange={(e) => setActualTotalCost(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.currency")}</Label>
          <Input value={currency} onChange={(e) => setCurrency(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>{t("licenseManagement:form.notes")}</Label>
          <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        <CheckboxField
          id="vat-included"
          label={t("licenseManagement:form.vatIncluded")}
          checked={vatIncluded}
          onCheckedChange={(checked) => setVatIncluded(checked === true)}
        />
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={mutation.isPending}>
          {t("common:actions.cancel")}
        </Button>
        <Button
          type="button"
          disabled={Boolean(validationKey) || mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {t("common:actions.save")}
        </Button>
      </div>
    </div>
  );
}
