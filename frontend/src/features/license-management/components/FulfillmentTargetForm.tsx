import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { DatePicker } from "@/components/common/DatePicker";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { getPurchaseTypeLabel, PURCHASE_TYPES } from "@/features/license-management/enum-labels";
import type {
  ConvertFulfillmentNewPurchase,
  LicenseCompanyListItem,
  LicensePurchaseListItem,
} from "@/features/license-management/types";

export type FulfillmentTargetKind = "new" | "existing";

type Props = {
  targetKind: FulfillmentTargetKind;
  onTargetKindChange: (kind: FulfillmentTargetKind) => void;
  newPurchase: ConvertFulfillmentNewPurchase;
  onNewPurchaseChange: (value: ConvertFulfillmentNewPurchase) => void;
  existingPurchaseId: string;
  onExistingPurchaseChange: (id: string) => void;
  companies: LicenseCompanyListItem[];
  purchases: LicensePurchaseListItem[];
  dateLocale: "tr" | "en";
  disabled?: boolean;
};

export function FulfillmentTargetForm({
  targetKind,
  onTargetKindChange,
  newPurchase,
  onNewPurchaseChange,
  existingPurchaseId,
  onExistingPurchaseChange,
  companies,
  purchases,
  dateLocale,
  disabled,
}: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  function patchNewPurchase(patch: Partial<ConvertFulfillmentNewPurchase>) {
    onNewPurchaseChange({ ...newPurchase, ...patch });
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-4">
        <label className="flex items-center gap-2 text-sm">
          <input
            type="radio"
            name="fulfillment-target"
            checked={targetKind === "new"}
            disabled={disabled}
            onChange={() => onTargetKindChange("new")}
          />
          {t("licenseManagement:requests.fulfillment.conversion.targetNew")}
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="radio"
            name="fulfillment-target"
            checked={targetKind === "existing"}
            disabled={disabled}
            onChange={() => onTargetKindChange("existing")}
          />
          {t("licenseManagement:requests.fulfillment.conversion.targetExisting")}
        </label>
      </div>

      {targetKind === "existing" ? (
        <div className="space-y-2">
          <Label htmlFor="fulfillment-existing-purchase">
            {t("licenseManagement:requests.fulfillment.conversion.selectPurchase")}
          </Label>
          <Select
            id="fulfillment-existing-purchase"
            value={existingPurchaseId}
            disabled={disabled}
            onChange={(event) => onExistingPurchaseChange(event.target.value)}
          >
            <option value="">{t("common:select.noOptions")}</option>
            {purchases.map((purchase) => (
              <option key={purchase.id} value={purchase.id}>
                {purchase.title}
              </option>
            ))}
          </Select>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="fulfillment-purchase-type">
              {t("licenseManagement:requests.fulfillment.newPurchase.purchaseType")}
            </Label>
            <Select
              id="fulfillment-purchase-type"
              value={newPurchase.purchaseType}
              disabled={disabled}
              onChange={(event) =>
                patchNewPurchase({
                  purchaseType: event.target.value as ConvertFulfillmentNewPurchase["purchaseType"],
                })
              }
            >
              {PURCHASE_TYPES.map((type) => (
                <option key={type} value={type}>
                  {getPurchaseTypeLabel(t, type)}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="fulfillment-purchase-title">
              {t("licenseManagement:requests.fulfillment.newPurchase.title")}
            </Label>
            <Input
              id="fulfillment-purchase-title"
              value={newPurchase.title}
              disabled={disabled}
              onChange={(event) => patchNewPurchase({ title: event.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label>{t("licenseManagement:requests.fulfillment.newPurchase.purchaseDate")}</Label>
            <DatePicker
              value={newPurchase.purchaseDate}
              onChange={(value) => patchNewPurchase({ purchaseDate: value })}
              placeholder={t("licenseManagement:requests.fulfillment.newPurchase.purchaseDate")}
              clearLabel={t("common:actions.clear")}
              locale={dateLocale}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="fulfillment-supplier-company">
              {t("licenseManagement:requests.fulfillment.newPurchase.supplierCompany")}
            </Label>
            <Select
              id="fulfillment-supplier-company"
              value={newPurchase.supplierCompanyId ?? ""}
              disabled={disabled}
              onChange={(event) =>
                patchNewPurchase({ supplierCompanyId: event.target.value || null })
              }
            >
              <option value="">{t("licenseManagement:requests.fulfillment.newPurchase.none")}</option>
              {companies.map((company) => (
                <option key={company.id} value={company.id}>
                  {company.name}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="fulfillment-support-company">
              {t("licenseManagement:requests.fulfillment.newPurchase.supportCompany")}
            </Label>
            <Select
              id="fulfillment-support-company"
              value={newPurchase.supportCompanyId ?? ""}
              disabled={disabled}
              onChange={(event) =>
                patchNewPurchase({ supportCompanyId: event.target.value || null })
              }
            >
              <option value="">{t("licenseManagement:requests.fulfillment.newPurchase.none")}</option>
              {companies.map((company) => (
                <option key={company.id} value={company.id}>
                  {company.name}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="fulfillment-actual-cost">
              {t("licenseManagement:requests.fulfillment.newPurchase.actualTotalCost")}
            </Label>
            <Input
              id="fulfillment-actual-cost"
              type="number"
              min="0"
              step="0.01"
              value={newPurchase.actualTotalCost ?? ""}
              disabled={disabled}
              onChange={(event) =>
                patchNewPurchase({
                  actualTotalCost: event.target.value === "" ? null : Number(event.target.value),
                })
              }
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="fulfillment-currency">
              {t("licenseManagement:requests.fulfillment.newPurchase.currency")}
            </Label>
            <Input
              id="fulfillment-currency"
              value={newPurchase.currency ?? ""}
              disabled={disabled}
              onChange={(event) => patchNewPurchase({ currency: event.target.value || null })}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="fulfillment-notes">
              {t("licenseManagement:requests.fulfillment.newPurchase.notes")}
            </Label>
            <Textarea
              id="fulfillment-notes"
              value={newPurchase.notes ?? ""}
              disabled={disabled}
              onChange={(event) => patchNewPurchase({ notes: event.target.value || null })}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <CheckboxField
              id="fulfillment-vat-included"
              label={t("licenseManagement:requests.fulfillment.newPurchase.vatIncluded")}
              checked={newPurchase.vatIncluded ?? false}
              disabled={disabled}
              onCheckedChange={(checked) => patchNewPurchase({ vatIncluded: checked })}
            />
          </div>
        </div>
      )}
    </div>
  );
}
