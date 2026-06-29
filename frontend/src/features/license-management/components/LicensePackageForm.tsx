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
  createLicensePackage,
  getAllLicensePurchases,
  getAllLicensedProducts,
  updateLicensePackage,
} from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  getPackageStatusLabel,
  LICENSE_TYPES,
  PACKAGE_STATUSES,
} from "@/features/license-management/enum-labels";
import { validatePackageForm } from "@/features/license-management/form-validation";
import type { LicensePackageDetail, LicensePackageStatus, LicenseType } from "@/features/license-management/types";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";

type Props = {
  mode: "create" | "edit";
  packageItem?: LicensePackageDetail | null;
  onCancel: () => void;
  onSaved: () => void;
};

function toDateOnly(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  return value.slice(0, 10);
}

export function LicensePackageForm({ mode, packageItem, onCancel, onSaved }: Props) {
  const { t, i18n } = useTranslation(["licenseManagement", "common"]);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const [purchaseId, setPurchaseId] = useState("");
  const [productId, setProductId] = useState("");
  const [licenseType, setLicenseType] = useState<LicenseType>("NamedUser");
  const [quantity, setQuantity] = useState("1");
  const [startDate, setStartDate] = useState<string | null>(null);
  const [endDate, setEndDate] = useState<string | null>(null);
  const [isPerpetual, setIsPerpetual] = useState(false);
  const [renewalRequired, setRenewalRequired] = useState(false);
  const [renewalDate, setRenewalDate] = useState<string | null>(null);
  const [serialNumber, setSerialNumber] = useState("");
  const [licenseKey, setLicenseKey] = useState("");
  const [licenseAccountEmail, setLicenseAccountEmail] = useState("");
  const [licensePortalUrl, setLicensePortalUrl] = useState("");
  const [licenseNotes, setLicenseNotes] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [status, setStatus] = useState<LicensePackageStatus>("Active");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const purchasesQuery = useQuery({
    queryKey: ["license-management", "purchases", "options"],
    queryFn: getAllLicensePurchases,
  });
  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "options"],
    queryFn: getAllLicensedProducts,
  });

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setPurchaseId(packageItem?.purchaseId ?? "");
    setProductId(packageItem?.productId ?? "");
    setLicenseType(packageItem?.licenseType ?? "NamedUser");
    setQuantity(String(packageItem?.quantity ?? 1));
    setStartDate(toDateOnly(packageItem?.startDate));
    setEndDate(toDateOnly(packageItem?.endDate));
    setIsPerpetual(packageItem?.isPerpetual ?? false);
    setRenewalRequired(packageItem?.renewalRequired ?? false);
    setRenewalDate(toDateOnly(packageItem?.renewalDate));
    setSerialNumber(packageItem?.serialNumber ?? "");
    setLicenseKey(packageItem?.licenseKey ?? "");
    setLicenseAccountEmail(packageItem?.licenseAccountEmail ?? "");
    setLicensePortalUrl(packageItem?.licensePortalUrl ?? "");
    setLicenseNotes(packageItem?.licenseNotes ?? "");
    setIsActive(packageItem?.isActive ?? true);
    setStatus(packageItem?.status ?? "Active");
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [packageItem]);

  const validationKey = useMemo(
    () => validatePackageForm(purchaseId, productId, Number(quantity)),
    [purchaseId, productId, quantity],
  );

  const showEndDateWarning = !isPerpetual && !endDate;

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        purchaseId,
        productId,
        licenseType,
        quantity: Number(quantity),
        startDate,
        endDate,
        isPerpetual,
        renewalRequired,
        renewalDate,
        serialNumber: serialNumber || null,
        licenseKey: licenseKey || null,
        licenseAccountEmail: licenseAccountEmail || null,
        licensePortalUrl: licensePortalUrl || null,
        licenseNotes: licenseNotes || null,
        isActive,
        status,
      };
      if (mode === "edit" && packageItem) {
        await updateLicensePackage(packageItem.id, payload);
      } else {
        await createLicensePackage(payload);
      }
    },
    onSuccess: () => {
      onSaved();
    },
    onError: (error) => {
      setErrorMessage(
        getLicenseManagementApiErrorMessage(
          error,
          t,
          "licenseManagement:messages.operationFailed",
        ),
      );
    },
  });

  return (
    <div className="space-y-4">
      {errorMessage ? <FormError message={errorMessage} /> : null}
      {validationKey ? <FormError message={t(`licenseManagement:messages.${validationKey}`)} /> : null}
      {showEndDateWarning ? (
        <p className="text-sm text-amber-600 dark:text-amber-400">
          {t("licenseManagement:messages.endDateWarning")}
        </p>
      ) : null}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.purchase")}</Label>
          <Select value={purchaseId} onChange={(e) => setPurchaseId(e.target.value)}>
            <option value="">{t("licenseManagement:form.selectPurchase")}</option>
            {(purchasesQuery.data ?? []).map((item) => (
              <option key={item.id} value={item.id}>{item.title}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.product")}</Label>
          <Select value={productId} onChange={(e) => setProductId(e.target.value)}>
            <option value="">{t("licenseManagement:form.selectProduct")}</option>
            {(productsQuery.data ?? []).map((item) => (
              <option key={item.id} value={item.id}>{item.name}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.licenseType")}</Label>
          <Select value={licenseType} onChange={(e) => setLicenseType(e.target.value as LicenseType)}>
            {LICENSE_TYPES.map((type) => (
              <option key={type} value={type}>{getLicenseTypeLabel(t, type)}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.quantity")}</Label>
          <Input type="number" min="1" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.startDate")}</Label>
          <DatePicker
            value={startDate}
            onChange={setStartDate}
            placeholder={t("licenseManagement:form.startDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.endDate")}</Label>
          <DatePicker
            value={endDate}
            onChange={setEndDate}
            placeholder={t("licenseManagement:form.endDate")}
            clearLabel={t("common:actions.clear")}
            locale={dateLocale}
            disabled={isPerpetual}
          />
        </div>
        <CheckboxField id="is-perpetual" label={t("licenseManagement:form.isPerpetual")} checked={isPerpetual} onCheckedChange={(c) => setIsPerpetual(c === true)} />
        <CheckboxField id="renewal-required" label={t("licenseManagement:form.renewalRequired")} checked={renewalRequired} onCheckedChange={(c) => setRenewalRequired(c === true)} />
        {renewalRequired ? (
          <div className="space-y-2">
            <Label>{t("licenseManagement:form.renewalDate")}</Label>
            <DatePicker
              value={renewalDate}
              onChange={setRenewalDate}
              placeholder={t("licenseManagement:form.renewalDate")}
              clearLabel={t("common:actions.clear")}
              locale={dateLocale}
            />
          </div>
        ) : null}
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.serialNumber")}</Label>
          <Input value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>{t("licenseManagement:form.licenseKey")}</Label>
          <Input value={licenseKey} onChange={(e) => setLicenseKey(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.licenseAccountEmail")}</Label>
          <Input value={licenseAccountEmail} onChange={(e) => setLicenseAccountEmail(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.licensePortalUrl")}</Label>
          <Input value={licensePortalUrl} onChange={(e) => setLicensePortalUrl(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>{t("licenseManagement:form.status")}</Label>
          <Select value={status} onChange={(e) => setStatus(e.target.value as LicensePackageStatus)}>
            {PACKAGE_STATUSES.map((item) => (
              <option key={item} value={item}>{getPackageStatusLabel(t, item)}</option>
            ))}
          </Select>
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>{t("licenseManagement:form.licenseNotes")}</Label>
          <Textarea value={licenseNotes} onChange={(e) => setLicenseNotes(e.target.value)} />
        </div>
        <CheckboxField id="package-active" label={t("common:status.active")} checked={isActive} onCheckedChange={(c) => setIsActive(c === true)} />
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
