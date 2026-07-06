import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { getLicenseTypeLabel, LICENSE_TYPES } from "@/features/license-management/enum-labels";
import type { ConvertFulfillmentPackageDefaults } from "@/features/license-management/types";

type ProductDefaultsRow = ConvertFulfillmentPackageDefaults & { productName: string };

type Props = {
  defaults: ProductDefaultsRow[];
  onChange: (productId: string, patch: Partial<ConvertFulfillmentPackageDefaults>) => void;
  dateLocale: "tr" | "en";
  disabled?: boolean;
};

export function FulfillmentPackageDefaultsForm({ defaults, onChange, dateLocale, disabled }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  return (
    <div className="space-y-4">
      {defaults.map((row) => (
        <div key={row.productId} className="space-y-3 rounded-lg border bg-card p-4">
          <p className="text-sm font-semibold">
            {t("licenseManagement:requests.fulfillment.packageDefaults.perProduct", {
              product: row.productName,
            })}
          </p>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor={`fulfillment-license-type-${row.productId}`}>
                {t("licenseManagement:requests.fulfillment.packageDefaults.licenseType")}
              </Label>
              <Select
                id={`fulfillment-license-type-${row.productId}`}
                value={row.licenseType}
                disabled={disabled}
                onChange={(event) =>
                  onChange(row.productId, {
                    licenseType: event.target.value as ConvertFulfillmentPackageDefaults["licenseType"],
                  })
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
                id={`fulfillment-perpetual-${row.productId}`}
                label={t("licenseManagement:requests.fulfillment.packageDefaults.isPerpetual")}
                checked={row.isPerpetual}
                disabled={disabled}
                onCheckedChange={(checked) =>
                  onChange(row.productId, {
                    isPerpetual: checked,
                    endDate: checked ? null : row.endDate,
                  })
                }
              />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:requests.fulfillment.packageDefaults.startDate")}</Label>
              <DatePicker
                value={row.startDate}
                onChange={(value) => onChange(row.productId, { startDate: value })}
                placeholder={t("licenseManagement:requests.fulfillment.packageDefaults.startDate")}
                clearLabel={t("common:actions.clear")}
                locale={dateLocale}
                disabled={disabled}
              />
            </div>
            <div className="space-y-2">
              <Label>{t("licenseManagement:requests.fulfillment.packageDefaults.endDate")}</Label>
              <DatePicker
                value={row.endDate}
                onChange={(value) => onChange(row.productId, { endDate: value })}
                placeholder={t("licenseManagement:requests.fulfillment.packageDefaults.endDate")}
                clearLabel={t("common:actions.clear")}
                locale={dateLocale}
                disabled={disabled || row.isPerpetual}
              />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
