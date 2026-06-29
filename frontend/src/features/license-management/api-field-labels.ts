import type { TFunction } from "i18next";

const LICENSE_FIELD_LABEL_KEYS: Record<string, string> = {
  defaultLicenseType: "licenseManagement:form.licenseType",
  purchaseType: "licenseManagement:form.purchaseType",
  licenseType: "licenseManagement:form.licenseType",
  status: "licenseManagement:form.status",
  name: "licenseManagement:form.productName",
  title: "licenseManagement:form.title",
  vendorCompanyId: "licenseManagement:form.vendorCompany",
  supplierCompanyId: "licenseManagement:form.supplierCompany",
  supportCompanyId: "licenseManagement:form.supportCompany",
  productId: "licenseManagement:form.product",
  purchaseId: "licenseManagement:form.purchase",
  defaultCurrency: "licenseManagement:settings.defaultCurrency",
  defaultRenewalReminderDays: "licenseManagement:settings.defaultRenewalReminderDays",
};

export function resolveLicenseManagementFieldLabel(t: TFunction, fieldPath: string): string | null {
  const normalized = fieldPath
    .replace(/^\$\./, "")
    .replace(/^request\./, "")
    .replace(/^request$/, "")
    .trim();

  if (!normalized || normalized === "request") {
    return null;
  }

  const key = LICENSE_FIELD_LABEL_KEYS[normalized];
  return key ? t(key) : null;
}
