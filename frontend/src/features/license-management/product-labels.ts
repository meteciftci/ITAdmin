import type { LicensedProductListItem } from "@/features/license-management/types";

type ProductLabelSource = Pick<LicensedProductListItem, "name" | "brand" | "categoryName">;

export function formatLicensedProductLabel(product: ProductLabelSource): string {
  return [product.name, product.brand, product.categoryName].filter(Boolean).join(" — ");
}
