import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { getLicenseTypeLabel, LICENSE_TYPES } from "@/features/license-management/enum-labels";
import { formatLicensedProductLabel } from "@/features/license-management/product-labels";
import {
  createManualLineDraft,
  type ManualLineDraft,
} from "@/features/license-management/components/manual-line-draft";
import type { LicensedProductListItem, LicenseType } from "@/features/license-management/types";

type Props = {
  rows: ManualLineDraft[];
  onChange: (rows: ManualLineDraft[]) => void;
  products: LicensedProductListItem[];
  dateLocale: "tr" | "en";
  disabled?: boolean;
};

export function ManualLinesSection({ rows, onChange, products, dateLocale, disabled }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  function patchRow(key: string, patch: Partial<ManualLineDraft>) {
    onChange(rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  return (
    <div className="space-y-4">
      {rows.map((row) => (
        <div key={row.key} className="space-y-3 rounded-lg border bg-card p-4">
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm font-semibold">
              {t("licenseManagement:requests.fulfillment.manual.rowTitle")}
            </p>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={disabled}
              onClick={() => onChange(rows.filter((other) => other.key !== row.key))}
            >
              {t("common:actions.remove")}
            </Button>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor={`manual-product-${row.key}`}>
                {t("licenseManagement:requests.fulfillment.manual.product")}
              </Label>
              <Select
                id={`manual-product-${row.key}`}
                value={row.productId}
                disabled={disabled}
                onChange={(event) => patchRow(row.key, { productId: event.target.value })}
              >
                <option value="">{t("licenseManagement:requests.fulfillment.manual.productPlaceholder")}</option>
                {products.map((product) => (
                  <option key={product.id} value={product.id}>
                    {formatLicensedProductLabel(product)}
                  </option>
                ))}
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor={`manual-qty-${row.key}`}>
                {t("licenseManagement:requests.fulfillment.manual.quantity")}
              </Label>
              <Input
                id={`manual-qty-${row.key}`}
                type="number"
                min="1"
                disabled={disabled}
                value={row.quantity}
                onChange={(event) =>
                  patchRow(row.key, {
                    quantity: Math.max(1, Math.floor(Number(event.target.value) || 1)),
                  })
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor={`manual-type-${row.key}`}>
                {t("licenseManagement:requests.fulfillment.manual.licenseType")}
              </Label>
              <Select
                id={`manual-type-${row.key}`}
                value={row.licenseType}
                disabled={disabled}
                onChange={(event) =>
                  patchRow(row.key, { licenseType: event.target.value as LicenseType })
                }
              >
                {LICENSE_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {getLicenseTypeLabel(t, type)}
                  </option>
                ))}
              </Select>
            </div>

            <div className="space-y-2 md:col-span-2">
              <CheckboxField
                id={`manual-perpetual-${row.key}`}
                label={t("licenseManagement:requests.fulfillment.manual.isPerpetual")}
                checked={row.isPerpetual}
                disabled={disabled}
                onCheckedChange={(checked) =>
                  patchRow(row.key, { isPerpetual: checked, endDate: checked ? null : row.endDate })
                }
              />
            </div>

            <div className="space-y-2">
              <Label>{t("licenseManagement:requests.fulfillment.manual.startDate")}</Label>
              <DatePicker
                value={row.startDate}
                onChange={(value) => patchRow(row.key, { startDate: value })}
                placeholder={t("licenseManagement:requests.fulfillment.manual.startDate")}
                clearLabel={t("common:actions.clear")}
                locale={dateLocale}
                disabled={disabled}
              />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:requests.fulfillment.manual.endDate")}</Label>
              <DatePicker
                value={row.endDate}
                onChange={(value) => patchRow(row.key, { endDate: value })}
                placeholder={t("licenseManagement:requests.fulfillment.manual.endDate")}
                clearLabel={t("common:actions.clear")}
                locale={dateLocale}
                disabled={disabled || row.isPerpetual}
              />
            </div>
          </div>
        </div>
      ))}

      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled}
        onClick={() => onChange([...rows, createManualLineDraft()])}
      >
        {t("licenseManagement:requests.fulfillment.manual.addRow")}
      </Button>
    </div>
  );
}
