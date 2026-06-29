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

export function validateCompanyForm(name: string, email: string, supportEmail: string, contactEmail: string, website: string) {
  if (!name.trim()) {
    return "nameRequired";
  }
  if (!isValidEmail(email) || !isValidEmail(supportEmail) || !isValidEmail(contactEmail)) {
    return "invalidEmail";
  }
  if (!isValidUrl(website)) {
    return "invalidUrl";
  }
  return null;
}

export function validateProductForm(name: string) {
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

export function validatePackageForm(purchaseId: string, productId: string, quantity: number) {
  if (!purchaseId) {
    return "purchaseRequired";
  }
  if (!productId) {
    return "productRequired";
  }
  if (!Number.isFinite(quantity) || quantity < 1) {
    return "quantityMin";
  }
  return null;
}
