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
  createLicenseAcquisition,
  getAllLicenseCompanies,
  updateLicenseAcquisition,
} from "@/features/license-management/api";
import {
  ACQUISITION_STATUSES,
  ACQUISITION_TYPES,
  getAcquisitionStatusLabel,
  getAcquisitionTypeLabel,
} from "@/features/license-management/enum-labels";
import { validateAcquisitionForm } from "@/features/license-management/form-validation";
import type {
  LicenseAcquisitionDetail,
  LicenseAcquisitionStatus,
  LicenseAcquisitionType,
} from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type Props = {
  open: boolean;
  mode: "create" | "edit";
  acquisition?: LicenseAcquisitionDetail | null;
  onClose: () => void;
  onSaved: () => void;
};

export function AcquisitionFormDialog({ open, mode, acquisition, onClose, onSaved }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [acquisitionType, setAcquisitionType] = useState<LicenseAcquisitionType>("DirectPurchase");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [acquisitionDate, setAcquisitionDate] = useState("");
  const [contractNumber, setContractNumber] = useState("");
  const [contractStartDate, setContractStartDate] = useState("");
  const [contractEndDate, setContractEndDate] = useState("");
  const [supplierCompanyId, setSupplierCompanyId] = useState("");
  const [supportCompanyId, setSupportCompanyId] = useState("");
  const [actualTotalCost, setActualTotalCost] = useState("");
  const [currency, setCurrency] = useState("");
  const [vatIncluded, setVatIncluded] = useState(false);
  const [notes, setNotes] = useState("");
  const [status, setStatus] = useState<LicenseAcquisitionStatus>("Draft");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const companiesQuery = useQuery({
    queryKey: ["license-management", "companies", "options"],
    queryFn: getAllLicenseCompanies,
    enabled: open,
  });

  useEffect(() => {
    if (!open) {
      return;
    }

    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when dialog opens */
    setAcquisitionType(acquisition?.acquisitionType ?? "DirectPurchase");
    setTitle(acquisition?.title ?? "");
    setDescription(acquisition?.description ?? "");
    setAcquisitionDate(acquisition?.acquisitionDate?.slice(0, 10) ?? "");
    setContractNumber(acquisition?.contractNumber ?? "");
    setContractStartDate(acquisition?.contractStartDate?.slice(0, 10) ?? "");
    setContractEndDate(acquisition?.contractEndDate?.slice(0, 10) ?? "");
    setSupplierCompanyId(acquisition?.supplierCompanyId ?? "");
    setSupportCompanyId(acquisition?.supportCompanyId ?? "");
    setActualTotalCost(acquisition?.actualTotalCost?.toString() ?? "");
    setCurrency(acquisition?.currency ?? "");
    setVatIncluded(acquisition?.vatIncluded ?? false);
    setNotes(acquisition?.notes ?? "");
    setStatus(acquisition?.status ?? "Draft");
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [open, acquisition]);

  const validationKey = useMemo(() => validateAcquisitionForm(title), [title]);

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        acquisitionType,
        title: title.trim(),
        description: description || null,
        acquisitionDate: acquisitionDate || null,
        contractNumber: contractNumber || null,
        contractStartDate: contractStartDate || null,
        contractEndDate: contractEndDate || null,
        supplierCompanyId: supplierCompanyId || null,
        supportCompanyId: supportCompanyId || null,
        actualTotalCost: actualTotalCost ? Number(actualTotalCost) : null,
        currency: currency || null,
        vatIncluded,
        notes: notes || null,
        status,
      };
      if (mode === "edit" && acquisition) {
        await updateLicenseAcquisition(acquisition.id, payload);
      } else {
        await createLicenseAcquisition(payload);
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
              ? t("licenseManagement:actions.editAcquisition")
              : t("licenseManagement:actions.addAcquisition")}
          </DialogTitle>
        </DialogHeader>
        <DialogBody className="space-y-4">
          {errorMessage ? <FormError message={errorMessage} /> : null}
          {validationKey ? <FormError message={t(`licenseManagement:messages.${validationKey}`)} /> : null}
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.acquisitionType")}</Label>
              <Select value={acquisitionType} onChange={(e) => setAcquisitionType(e.target.value as LicenseAcquisitionType)}>
                {ACQUISITION_TYPES.map((type) => (
                  <option key={type} value={type}>{getAcquisitionTypeLabel(t, type)}</option>
                ))}
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.status")}</Label>
              <Select value={status} onChange={(e) => setStatus(e.target.value as LicenseAcquisitionStatus)}>
                {ACQUISITION_STATUSES.map((item) => (
                  <option key={item} value={item}>{getAcquisitionStatusLabel(t, item)}</option>
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
              <Label>{t("licenseManagement:form.acquisitionDate")}</Label>
              <Input type="date" value={acquisitionDate} onChange={(e) => setAcquisitionDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.contractNumber")}</Label>
              <Input value={contractNumber} onChange={(e) => setContractNumber(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.contractStartDate")}</Label>
              <Input type="date" value={contractStartDate} onChange={(e) => setContractStartDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.contractEndDate")}</Label>
              <Input type="date" value={contractEndDate} onChange={(e) => setContractEndDate(e.target.value)} />
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
