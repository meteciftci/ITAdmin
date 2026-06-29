import type { LicenseType } from "@/features/license-management/types";

export type LicensedProductFormValues = {
  name: string;
  vendorCompanyId: string;
  category: string;
  defaultLicenseType: LicenseType | "";
  description: string;
  notes: string;
  isActive: boolean;
};

export function buildLicensedProductPayload(values: LicensedProductFormValues) {
  return {
    name: values.name.trim(),
    vendorCompanyId: values.vendorCompanyId || null,
    category: values.category || null,
    defaultLicenseType: values.defaultLicenseType || null,
    description: values.description || null,
    notes: values.notes || null,
    isActive: values.isActive,
  };
}
