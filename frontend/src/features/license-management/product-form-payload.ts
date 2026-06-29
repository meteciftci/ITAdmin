export type LicensedProductFormValues = {
  name: string;
  brand: string;
  categoryId: string;
  description: string;
  isActive: boolean;
};

export function buildLicensedProductPayload(values: LicensedProductFormValues) {
  return {
    name: values.name.trim(),
    brand: values.brand.trim() || null,
    categoryId: values.categoryId,
    description: values.description.trim() || null,
    isActive: values.isActive,
  };
}
