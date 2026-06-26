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
  createLicensedProduct,
  getAllLicenseCompanies,
  updateLicensedProduct,
} from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  LICENSE_TYPES,
} from "@/features/license-management/enum-labels";
import { validateProductForm } from "@/features/license-management/form-validation";
import type { LicensedProductDetail, LicenseType } from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type Props = {
  open: boolean;
  mode: "create" | "edit";
  product?: LicensedProductDetail | null;
  onClose: () => void;
  onSaved: () => void;
};

export function ProductFormDialog({ open, mode, product, onClose, onSaved }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [name, setName] = useState("");
  const [vendorCompanyId, setVendorCompanyId] = useState("");
  const [category, setCategory] = useState("");
  const [defaultLicenseType, setDefaultLicenseType] = useState<LicenseType | "">("");
  const [description, setDescription] = useState("");
  const [notes, setNotes] = useState("");
  const [isActive, setIsActive] = useState(true);
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
    setName(product?.name ?? "");
    setVendorCompanyId(product?.vendorCompanyId ?? "");
    setCategory(product?.category ?? "");
    setDefaultLicenseType(product?.defaultLicenseType ?? "");
    setDescription(product?.description ?? "");
    setNotes(product?.notes ?? "");
    setIsActive(product?.isActive ?? true);
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [open, product]);

  const validationKey = useMemo(() => validateProductForm(name), [name]);

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name: name.trim(),
        vendorCompanyId: vendorCompanyId || null,
        category: category || null,
        defaultLicenseType: defaultLicenseType || null,
        description: description || null,
        notes: notes || null,
        isActive,
      };
      if (mode === "edit" && product) {
        await updateLicensedProduct(product.id, payload);
      } else {
        await createLicensedProduct(payload);
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
      <DialogContent className="max-w-2xl" onOpenChange={(next) => !next && onClose()}>
        <DialogHeader>
          <DialogTitle>
            {mode === "edit"
              ? t("licenseManagement:actions.editProduct")
              : t("licenseManagement:actions.addProduct")}
          </DialogTitle>
        </DialogHeader>
        <DialogBody className="space-y-4">
          {errorMessage ? <FormError message={errorMessage} /> : null}
          {validationKey ? <FormError message={t(`licenseManagement:messages.${validationKey}`)} /> : null}
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>{t("licenseManagement:form.productName")}</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>{t("licenseManagement:form.vendorCompany")}</Label>
              <Select value={vendorCompanyId} onChange={(e) => setVendorCompanyId(e.target.value)}>
                <option value="">{t("licenseManagement:form.noVendor")}</option>
                {(companiesQuery.data ?? []).map((company) => (
                  <option key={company.id} value={company.id}>
                    {company.name}
                  </option>
                ))}
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:table.category")}</Label>
              <Input value={category} onChange={(e) => setCategory(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:form.licenseType")}</Label>
              <Select
                value={defaultLicenseType}
                onChange={(e) => setDefaultLicenseType(e.target.value as LicenseType | "")}
              >
                <option value="">-</option>
                {LICENSE_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {getLicenseTypeLabel(t, type)}
                  </option>
                ))}
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>{t("licenseManagement:form.description")}</Label>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>{t("licenseManagement:form.notes")}</Label>
              <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
            <CheckboxField
              id="product-active"
              label={t("common:status.active")}
              checked={isActive}
              onCheckedChange={(checked) => setIsActive(checked === true)}
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
