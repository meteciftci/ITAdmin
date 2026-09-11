import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { Label } from "@/components/ui/label";
import { getLicenseTypeLabel } from "@/features/license-management/enum-labels";
import type { ConvertFulfillmentPackageDefaults } from "@/features/license-management/types";

type ProductDefaultsRow = ConvertFulfillmentPackageDefaults & { groupKey: string; productName: string };

type Props = {
  defaults: ProductDefaultsRow[];
  onChange: (
    groupKey: string,
    productId: string,
    licenseType: ConvertFulfillmentPackageDefaults["licenseType"],
    patch: Partial<ConvertFulfillmentPackageDefaults>,
  ) => void;
  dateLocale: "tr" | "en";
  disabled?: boolean;
};

export function FulfillmentPackageDefaultsForm({ defaults, onChange, dateLocale, disabled }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  return (
    <div className="space-y-4">
      {defaults.map((row) => (
        <div key={row.groupKey} className="space-y-3 rounded-lg border bg-card p-4">
          <p className="text-sm font-semibold">
            {t("licenseManagement:requests.fulfillment.packageDefaults.perProduct", {
              product: row.productName,
            })}
          </p>
          <p className="text-sm text-muted-foreground">{getLicenseTypeLabel(t, row.licenseType)}</p>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <CheckboxField
                id={`fulfillment-perpetual-${row.productId}`}
                label={t("licenseManagement:requests.fulfillment.packageDefaults.isPerpetual")}
                checked={row.isPerpetual}
                disabled={disabled}
                onCheckedChange={(checked) =>
                  onChange(row.groupKey, row.productId, row.licenseType, {
                    isPerpetual: checked,
                    endDate: checked ? null : row.endDate,
                  })
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor={`fulfillment-start-date-${row.groupKey}-${row.productId}`}>{t("licenseManagement:requests.fulfillment.packageDefaults.startDate")}</Label>
              <DatePicker
                id={`fulfillment-start-date-${row.groupKey}-${row.productId}`}
                value={row.startDate}
                onChange={(value) => onChange(row.groupKey, row.productId, row.licenseType, { startDate: value })}
                placeholder={t("licenseManagement:requests.fulfillment.packageDefaults.startDate")}
                clearLabel={t("common:actions.clear")}
                locale={dateLocale}
                disabled={disabled}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor={`fulfillment-end-date-${row.groupKey}-${row.productId}`}>{t("licenseManagement:requests.fulfillment.packageDefaults.endDate")}</Label>
              <DatePicker
                id={`fulfillment-end-date-${row.groupKey}-${row.productId}`}
                value={row.endDate}
                onChange={(value) => onChange(row.groupKey, row.productId, row.licenseType, { endDate: value })}
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
