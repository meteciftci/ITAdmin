const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const urlPattern = /^https?:\/\/.+/i;

export function isValidEmail(value: string): boolean {
  if (!value.trim()) {
    return true;
  }
  return emailPattern.test(value.trim());
}

export function isValidUrl(value: string): boolean {
  if (!value.trim()) {
    return true;
  }
  return urlPattern.test(value.trim());
}

export function validateCompanyForm(name: string, email: string, contactEmail: string, website: string) {
  if (!name.trim()) {
    return "nameRequired";
  }
  if (!isValidEmail(email) || !isValidEmail(contactEmail)) {
    return "invalidEmail";
  }
  if (!isValidUrl(website)) {
    return "invalidUrl";
  }
  return null;
}

export function validateProductForm(name: string, categoryId: string) {
  if (!name.trim()) {
    return "nameRequired";
  }
  if (!categoryId) {
    return "categoryRequired";
  }
  return null;
}

export function validateCategoryForm(name: string) {
  if (!name.trim()) {
    return "nameRequired";
  }
  return null;
}

export function validatePurchaseForm(title: string) {
  if (!title.trim()) {
    return "titleRequired";
  }
  return null;
}

export function validatePackageForm(
  purchaseId: string,
  productId: string,
  quantity: number,
  startDate: string | null = null,
  endDate: string | null = null,
  isPerpetual = false,
  renewalRequired = false,
  renewalDate: string | null = null,
) {
  if (!purchaseId) {
    return "purchaseRequired";
  }
  if (!productId) {
    return "productRequired";
  }
  if (!Number.isFinite(quantity) || quantity < 1) {
    return "quantityMin";
  }
  return validatePackageDateFields(
    startDate,
    endDate,
    isPerpetual,
    renewalRequired,
    renewalDate,
  );
}

export function validatePackageDateFields(
  startDate: string | null,
  endDate: string | null,
  isPerpetual: boolean,
  renewalRequired: boolean,
  renewalDate: string | null,
) {
  if (startDate && endDate && endDate < startDate) {
    return "invalidLicenseDateRange";
  }
  if (isPerpetual && endDate) {
    return "perpetualEndDateNotAllowed";
  }
  if (renewalRequired && !renewalDate) {
    return "renewalDateRequired";
  }
  if (!renewalRequired && renewalDate) {
    return "renewalDateNotAllowed";
  }
  if (renewalDate && startDate && renewalDate < startDate) {
    return "renewalDateBeforeStart";
  }
  if (renewalDate && endDate && renewalDate > endDate) {
    return "renewalDateAfterEnd";
  }
  return null;
}
