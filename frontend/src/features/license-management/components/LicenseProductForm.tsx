import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
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
import { buildLicensedProductPayload } from "@/features/license-management/product-form-payload";
import type { LicensedProductDetail, LicenseType } from "@/features/license-management/types";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";

type Props = {
  mode: "create" | "edit";
  product?: LicensedProductDetail | null;
  onCancel: () => void;
  onSaved: () => void;
};

export function LicenseProductForm({ mode, product, onCancel, onSaved }: Props) {
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
  });

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setName(product?.name ?? "");
    setVendorCompanyId(product?.vendorCompanyId ?? "");
    setCategory(product?.category ?? "");
    setDefaultLicenseType(product?.defaultLicenseType ?? "");
    setDescription(product?.description ?? "");
    setNotes(product?.notes ?? "");
    setIsActive(product?.isActive ?? true);
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [product]);

  const validationKey = useMemo(() => validateProductForm(name), [name]);

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = buildLicensedProductPayload({
        name,
        vendorCompanyId,
        category,
        defaultLicenseType,
        description,
        notes,
        isActive,
      });
      if (mode === "edit" && product) {
        await updateLicensedProduct(product.id, payload);
      } else {
        await createLicensedProduct(payload);
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
