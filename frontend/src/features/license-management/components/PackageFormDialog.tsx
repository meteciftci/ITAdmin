import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";

import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  createLicensePackage,
  getAllLicenseAcquisitions,
  getAllLicensedProducts,
  updateLicensePackage,
} from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  LICENSE_TYPES,
  PACKAGE_STATUSES,
  getPackageStatusLabel,
} from "@/features/license-management/enum-labels";
import { validatePackageForm } from "@/features/license-management/form-validation";
import type { LicensePackageDetail, LicensePackageStatus, LicenseType } from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type Props = {
  open: boolean;
  mode: "create" | "edit";
  packageItem?: LicensePackageDetail | null;
  onClose: () => void;
  onSaved: () => void;
};

export function PackageFormDialog({ open, mode, packageItem, onClose, onSaved }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [acquisitionId, setAcquisitionId] = useState("");
  const [productId, setProductId] = useState("");
  const [licenseType, setLicenseType] = useState<LicenseType>("NamedUser");
  const [quantity, setQuantity] = useState("1");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [isPerpetual, setIsPerpetual] = useState(false);
  const [renewalRequired, setRenewalRequired] = useState(false);
  const [renewalDate, setRenewalDate] = useState("");
  const [serialNumber, setSerialNumber] = useState("");
  const [licenseKey, setLicenseKey] = useState("");
  const [licenseAccountEmail, setLicenseAccountEmail] = useState("");
  const [licensePortalUrl, setLicensePortalUrl] = useState("");
  const [licenseNotes, setLicenseNotes] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [status, setStatus] = useState<LicensePackageStatus>("Active");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const acquisitionsQuery = useQuery({
    queryKey: ["license-management", "acquisitions", "options"],
    queryFn: getAllLicenseAcquisitions,
    enabled: open,
  });
  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "options"],
    queryFn: getAllLicensedProducts,
    enabled: open,
  });

  useEffect(() => {
    if (!open) {
      return;
    }

    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when dialog opens */
    setAcquisitionId(packageItem?.acquisitionId ?? "");
    setProductId(packageItem?.productId ?? "");
    setLicenseType(packageItem?.licenseType ?? "NamedUser");
    setQuantity(String(packageItem?.quantity ?? 1));
    setStartDate(packageItem?.startDate?.slice(0, 10) ?? "");
    setEndDate(packageItem?.endDate?.slice(0, 10) ?? "");
    setIsPerpetual(packageItem?.isPerpetual ?? false);
    setRenewalRequired(packageItem?.renewalRequired ?? false);
    setRenewalDate(packageItem?.renewalDate?.slice(0, 10) ?? "");
    setSerialNumber(packageItem?.serialNumber ?? "");
    setLicenseKey(packageItem?.licenseKey ?? "");
    setLicenseAccountEmail(packageItem?.licenseAccountEmail ?? "");
    setLicensePortalUrl(packageItem?.licensePortalUrl ?? "");
    setLicenseNotes(packageItem?.licenseNotes ?? "");
    setIsActive(packageItem?.isActive ?? true);
    setStatus(packageItem?.status ?? "Active");
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [open, packageItem]);

  const validationKey = useMemo(
    () => validatePackageForm(acquisitionId, productId, Number(quantity)),
    [acquisitionId, productId, quantity],
  );

  const showEndDateWarning = !isPerpetual && !endDate;

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        acquisitionId,
        productId,
        licenseType,
        quantity: Number(quantity),
        startDate: startDate || null,
        endDate: endDate || null,
        isPerpetual,
        renewalRequired,
        renewalDate: renewalDate || null,
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
      onClose();
    },
    onError: (error) => {
      setErrorMessage(getApiErrorMessage(error, t("licenseManagement:messages.operationFailed")));
    },
  });

  return (
    <Dialog open={open}>
      <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto" onOpenChange={(next) => !next && onClose()}>
        <DialogHeader>
          <DialogTitle>
            {mode === "edit"
              ? t("licenseManagement:actions.editPackage")
              : t("licenseManagement:actions.addPackage")}
          </DialogTitle>
        </DialogHeader>
        <DialogBody className="space-y-4">
          {errorMessage ? <FormError message={errorMessage} /> : null}
          {validationKey ? <FormError message={t(`licenseManagement:messages.${validationKey}`)} /> : null}
          {showEndDateWarning ? (
            <p className="text-sm text-amber-600 dark:text-amber-400">
              {t("licenseManagement:messages.endDateWarning")}
            </p>
          ) : null}
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.acquisition")}</Label>
              <Select value={acquisitionId} onChange={(e) => setAcquisitionId(e.target.value)}>
                <option value="">{t("licenseManagement:form.selectAcquisition")}</option>
                {(acquisitionsQuery.data ?? []).map((item) => (
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
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.endDate")}</Label>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} disabled={isPerpetual} />
            </div>
            <CheckboxField id="is-perpetual" label={t("licenseManagement:form.isPerpetual")} checked={isPerpetual} onCheckedChange={(c) => setIsPerpetual(c === true)} />
            <CheckboxField id="renewal-required" label={t("licenseManagement:form.renewalRequired")} checked={renewalRequired} onCheckedChange={(c) => setRenewalRequired(c === true)} />
            {renewalRequired ? (
              <div className="space-y-2">
                <Label>{t("licenseManagement:form.renewalDate")}</Label>
                <Input type="date" value={renewalDate} onChange={(e) => setRenewalDate(e.target.value)} />
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
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t("common:actions.cancel")}</Button>
          <Button disabled={Boolean(validationKey) || mutation.isPending} onClick={() => mutation.mutate()}>
            {t("common:actions.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
