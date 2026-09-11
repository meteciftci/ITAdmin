import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { getLicenseTypeLabel, LICENSE_TYPES } from "@/features/license-management/enum-labels";
import type {
  ConvertFulfillmentRenewalLine,
  LicensePackageListItem,
  LicenseType,
} from "@/features/license-management/types";

type Props = {
  rows: ConvertFulfillmentRenewalLine[];
  onChange: (rows: ConvertFulfillmentRenewalLine[]) => void;
  packages: LicensePackageListItem[];
  dateLocale: "tr" | "en";
  disabled?: boolean;
};

export function RenewalLinesSection({ rows, onChange, packages, dateLocale, disabled }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  const packageById = useMemo(() => {
    const map = new Map<string, LicensePackageListItem>();
    for (const pkg of packages) {
      map.set(pkg.id, pkg);
    }
    return map;
  }, [packages]);

  const available = useMemo(
    () => packages.filter(
      (pkg) => pkg.status !== "Cancelled"
        && pkg.status !== "Archived"
        && !rows.some((row) => row.sourcePackageId === pkg.id),
    ),
    [packages, rows],
  );

  function addRow(sourcePackageId: string) {
    const pkg = packageById.get(sourcePackageId);
    if (!pkg) {
      return;
    }
    onChange([
      ...rows,
      {
        sourcePackageId,
        quantity: pkg.quantity,
        licenseType: null,
        startDate: null,
        endDate: null,
        isPerpetual: false,
        expireSourcePackage: true,
        copySeatAssignments: pkg.licenseType === "NamedUser",
        renewalRequired: pkg.renewalRequired,
        renewalDate: null,
      },
    ]);
  }

  function patchRow(sourcePackageId: string, patch: Partial<ConvertFulfillmentRenewalLine>) {
    onChange(
      rows.map((row) => (row.sourcePackageId === sourcePackageId ? { ...row, ...patch } : row)),
    );
  }

  function removeRow(sourcePackageId: string) {
    onChange(rows.filter((row) => row.sourcePackageId !== sourcePackageId));
  }

  return (
    <div className="space-y-4">
      <div className="max-w-md space-y-1">
        <Label htmlFor="renewal-add">{t("licenseManagement:requests.fulfillment.renewal.addLabel")}</Label>
        <Select
          id="renewal-add"
          value=""
          disabled={disabled || available.length === 0}
          onChange={(event) => {
            if (event.target.value) {
              addRow(event.target.value);
            }
          }}
        >
          <option value="">{t("licenseManagement:requests.fulfillment.renewal.addPlaceholder")}</option>
          {available.map((pkg) => (
            <option key={pkg.id} value={pkg.id}>
              {pkg.productName} · {pkg.purchaseTitle} — {pkg.quantity}
            </option>
          ))}
        </Select>
      </div>

      {rows.map((row) => {
        const pkg = packageById.get(row.sourcePackageId);
        return (
          <div key={row.sourcePackageId} className="space-y-3 rounded-lg border bg-card p-4">
            <div className="flex items-start justify-between gap-2">
              <p className="text-sm font-semibold">
                {pkg ? `${pkg.productName} · ${pkg.purchaseTitle}` : row.sourcePackageId}
              </p>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                onClick={() => removeRow(row.sourcePackageId)}
              >
                {t("common:actions.remove")}
              </Button>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor={`renewal-qty-${row.sourcePackageId}`}>
                  {t("licenseManagement:requests.fulfillment.renewal.quantity")}
                </Label>
                <Input
                  id={`renewal-qty-${row.sourcePackageId}`}
                  type="number"
                  min="1"
                  disabled={disabled}
                  value={row.quantity}
                  onChange={(event) =>
                    patchRow(row.sourcePackageId, {
                      quantity: Math.max(1, Math.floor(Number(event.target.value) || 1)),
                    })
                  }
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor={`renewal-type-${row.sourcePackageId}`}>
                  {t("licenseManagement:requests.fulfillment.renewal.licenseType")}
                </Label>
                <Select
                  id={`renewal-type-${row.sourcePackageId}`}
                  value={row.licenseType ?? ""}
                  disabled={disabled}
                  onChange={(event) =>
                    patchRow(row.sourcePackageId, {
                      licenseType: event.target.value ? (event.target.value as LicenseType) : null,
                      copySeatAssignments: (event.target.value || pkg?.licenseType) === "NamedUser"
                        ? row.copySeatAssignments
                        : false,
                    })
                  }
                >
                  <option value="">
                    {t("licenseManagement:requests.fulfillment.renewal.licenseTypeInherit")}
                  </option>
                  {LICENSE_TYPES.map((type) => (
                    <option key={type} value={type}>
                      {getLicenseTypeLabel(t, type)}
                    </option>
                  ))}
                </Select>
              </div>

              <div className="space-y-2 md:col-span-2">
                <CheckboxField
                  id={`renewal-required-${row.sourcePackageId}`}
                  label={t("licenseManagement:requests.fulfillment.renewal.renewalRequired")}
                  checked={row.renewalRequired}
                  disabled={disabled}
                  onCheckedChange={(checked) =>
                    patchRow(row.sourcePackageId, {
                      renewalRequired: checked,
                      renewalDate: checked ? row.renewalDate : null,
                    })
                  }
                />
                {row.renewalRequired ? (
                  <div className="space-y-2 pt-2">
                    <Label htmlFor={`renewal-date-${row.sourcePackageId}`}>{t("licenseManagement:requests.fulfillment.renewal.renewalDate")}</Label>
                    <DatePicker
                      id={`renewal-date-${row.sourcePackageId}`}
                      value={row.renewalDate}
                      onChange={(value) => patchRow(row.sourcePackageId, { renewalDate: value })}
                      placeholder={t("licenseManagement:requests.fulfillment.renewal.renewalDate")}
                      clearLabel={t("common:actions.clear")}
                      locale={dateLocale}
                      disabled={disabled}
                    />
                  </div>
                ) : null}
              </div>

              <div className="space-y-2 md:col-span-2">
                <CheckboxField
                  id={`renewal-perpetual-${row.sourcePackageId}`}
                  label={t("licenseManagement:requests.fulfillment.renewal.isPerpetual")}
                  checked={row.isPerpetual}
                  disabled={disabled}
                  onCheckedChange={(checked) =>
                    patchRow(row.sourcePackageId, {
                      isPerpetual: checked,
                      endDate: checked ? null : row.endDate,
                    })
                  }
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor={`renewal-start-date-${row.sourcePackageId}`}>{t("licenseManagement:requests.fulfillment.renewal.startDate")}</Label>
                <DatePicker
                  id={`renewal-start-date-${row.sourcePackageId}`}
                  value={row.startDate}
                  onChange={(value) => patchRow(row.sourcePackageId, { startDate: value })}
                  placeholder={t("licenseManagement:requests.fulfillment.renewal.startDate")}
                  clearLabel={t("common:actions.clear")}
                  locale={dateLocale}
                  disabled={disabled}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor={`renewal-end-date-${row.sourcePackageId}`}>{t("licenseManagement:requests.fulfillment.renewal.endDate")}</Label>
                <DatePicker
                  id={`renewal-end-date-${row.sourcePackageId}`}
                  value={row.endDate}
                  onChange={(value) => patchRow(row.sourcePackageId, { endDate: value })}
                  placeholder={t("licenseManagement:requests.fulfillment.renewal.endDate")}
                  clearLabel={t("common:actions.clear")}
                  locale={dateLocale}
                  disabled={disabled || row.isPerpetual}
                />
              </div>

              <div className="space-y-2 md:col-span-2">
                <CheckboxField
                  id={`renewal-expire-${row.sourcePackageId}`}
                  label={t("licenseManagement:requests.fulfillment.renewal.expireSource")}
                  checked={row.expireSourcePackage}
                  disabled={disabled
                    || pkg?.status === "Active"
                    || pkg?.status === "Suspended"}
                  onCheckedChange={(checked) =>
                    patchRow(row.sourcePackageId, { expireSourcePackage: checked })
                  }
                />
                <CheckboxField
                  id={`renewal-seats-${row.sourcePackageId}`}
                  label={t("licenseManagement:requests.fulfillment.renewal.copySeats")}
                  checked={row.copySeatAssignments}
                  disabled={disabled
                    || pkg?.licenseType !== "NamedUser"
                    || (row.licenseType ?? pkg?.licenseType) !== "NamedUser"}
                  onCheckedChange={(checked) =>
                    patchRow(row.sourcePackageId, { copySeatAssignments: checked })
                  }
                />
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
