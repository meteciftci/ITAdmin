import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { LicenseAdUserMultiSelect } from "@/features/license-management/components/LicenseAdUserMultiSelect";
import {
  formatRequestUserCountLabel,
} from "@/features/license-management/license-request-payload";
import {
  getRequestItemStatusLabel,
  MANUAL_REQUEST_ITEM_STATUSES,
} from "@/features/license-management/enum-labels";
import { formatLicensedProductLabel } from "@/features/license-management/product-labels";
import type { LicenseRequestItemDraft } from "@/features/license-management/license-request-payload";
import type { LicensedProductListItem } from "@/features/license-management/types";

type Props = {
  item: LicenseRequestItemDraft;
  index: number;
  products: LicensedProductListItem[];
  usedProductIds: Set<string>;
  disabled?: boolean;
  onChange: (item: LicenseRequestItemDraft) => void;
  onRemove: () => void;
};

export function LicenseRequestItemCard({
  item,
  index,
  products,
  usedProductIds,
  disabled,
  onChange,
  onRemove,
}: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  const productOptions = useMemo(
    () =>
      products.filter(
        (product) => product.id === item.productId || !usedProductIds.has(product.id),
      ),
    [item.productId, products, usedProductIds],
  );

  return (
    <div className="space-y-4 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="text-sm font-semibold">
          {t("licenseManagement:requests.fields.itemTitle", { index: index + 1 })}
        </h4>
        <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={onRemove}>
          {t("common:actions.remove")}
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={`request-item-product-${item.clientId}`}>
            {t("licenseManagement:requests.fields.product")}
          </Label>
          <Select
            id={`request-item-product-${item.clientId}`}
            value={item.productId}
            disabled={disabled}
            onChange={(event) => onChange({ ...item, productId: event.target.value })}
          >
            <option value="">{t("common:select.noOptions")}</option>
            {productOptions.map((product) => (
              <option key={product.id} value={product.id}>
                {formatLicensedProductLabel(product)}
              </option>
            ))}
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor={`request-item-status-${item.clientId}`}>
            {t("common:fields.status")}
          </Label>
          <Select
            id={`request-item-status-${item.clientId}`}
            value={item.status}
            disabled={disabled}
            onChange={(event) =>
              onChange({
                ...item,
                status: event.target.value as LicenseRequestItemDraft["status"],
              })
            }
          >
            {MANUAL_REQUEST_ITEM_STATUSES.map((status) => (
              <option key={status} value={status}>
                {getRequestItemStatusLabel(t, status)}
              </option>
            ))}
          </Select>
        </div>

        <div className="space-y-2 md:col-span-2">
          <Label htmlFor={`request-item-justification-${item.clientId}`}>
            {t("licenseManagement:requests.fields.justification")}
          </Label>
          <Input
            id={`request-item-justification-${item.clientId}`}
            value={item.justification}
            disabled={disabled}
            onChange={(event) => onChange({ ...item, justification: event.target.value })}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor={`request-item-unit-cost-${item.clientId}`}>
            {t("licenseManagement:requests.fields.estimatedUnitCost")}
          </Label>
          <Input
            id={`request-item-unit-cost-${item.clientId}`}
            type="number"
            min="0"
            step="0.01"
            value={item.estimatedUnitCost}
            disabled={disabled}
            onChange={(event) => onChange({ ...item, estimatedUnitCost: event.target.value })}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor={`request-item-currency-${item.clientId}`}>
            {t("licenseManagement:requests.fields.currency")}
          </Label>
          <Input
            id={`request-item-currency-${item.clientId}`}
            value={item.currency}
            disabled={disabled}
            onChange={(event) => onChange({ ...item, currency: event.target.value })}
          />
        </div>

        <div className="space-y-2 md:col-span-2">
          <CheckboxField
            id={`request-item-vat-${item.clientId}`}
            label={t("licenseManagement:requests.fields.vatIncluded")}
            checked={item.vatIncluded}
            disabled={disabled}
            onCheckedChange={(checked) => onChange({ ...item, vatIncluded: checked })}
          />
        </div>
      </div>

      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">
          {formatRequestUserCountLabel(t, item.users.length)}
        </p>
        <LicenseAdUserMultiSelect
          users={item.users}
          onChange={(users) => onChange({ ...item, users })}
          disabled={disabled}
          label={t("licenseManagement:requests.actions.addUser")}
          placeholder={t("licenseManagement:requests.placeholders.selectAdUser")}
          searchPlaceholder={t("licenseManagement:requests.placeholders.searchAdUser")}
        />
      </div>
    </div>
  );
}
