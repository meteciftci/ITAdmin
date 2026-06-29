import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { LicenseRequestItemCard } from "@/features/license-management/components/LicenseRequestItemCard";
import {
  createEmptyRequestItemDraft,
  type LicenseRequestItemDraft,
} from "@/features/license-management/license-request-payload";
import type { LicensedProductListItem } from "@/features/license-management/types";

type Props = {
  items: LicenseRequestItemDraft[];
  products: LicensedProductListItem[];
  defaultCurrency: string;
  disabled?: boolean;
  onChange: (items: LicenseRequestItemDraft[]) => void;
};

export function LicenseRequestItemsEditor({
  items,
  products,
  defaultCurrency,
  disabled,
  onChange,
}: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  const usedProductIds = useMemo(
    () => new Set(items.map((item) => item.productId).filter(Boolean)),
    [items],
  );

  const canAddItem = products.some((product) => !usedProductIds.has(product.id));

  return (
    <div className="space-y-4">
      {items.map((item, index) => (
        <LicenseRequestItemCard
          key={item.clientId}
          item={item}
          index={index}
          products={products}
          usedProductIds={usedProductIds}
          disabled={disabled}
          onChange={(nextItem) =>
            onChange(items.map((current) => (current.clientId === item.clientId ? nextItem : current)))
          }
          onRemove={() => onChange(items.filter((current) => current.clientId !== item.clientId))}
        />
      ))}

      <Button
        type="button"
        variant="outline"
        disabled={disabled || !canAddItem}
        onClick={() => onChange([...items, createEmptyRequestItemDraft(defaultCurrency)])}
      >
        {t("licenseManagement:requests.actions.addItem")}
      </Button>
    </div>
  );
}
