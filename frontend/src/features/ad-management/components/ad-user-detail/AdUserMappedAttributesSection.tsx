import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { Switch } from "@/components/ui/switch";
import { filterMappedAttributesForDisplay, formatMappedAdUserAttributeValue } from "@/features/ad-management/ad-user-detail-utils";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
  showEmptyFields: boolean;
  onShowEmptyFieldsChange: (value: boolean) => void;
};

export function AdUserMappedAttributesSection({
  user,
  showEmptyFields,
  onShowEmptyFieldsChange,
}: Props) {
  const { t } = useTranslation("adManagement");

  const mappedAttributes = useMemo(
    () => filterMappedAttributesForDisplay(user.mappedAttributes, showEmptyFields),
    [showEmptyFields, user.mappedAttributes],
  );

  return (
    <SectionCard
      title={t("users.detail.page.mappedAttributes")}
      actions={
        <label className="flex cursor-pointer items-center gap-2 text-sm">
          <Switch
            checked={showEmptyFields}
            onCheckedChange={onShowEmptyFieldsChange}
            aria-label={t("users.detail.page.showEmptyFields")}
          />
          <span>{t("users.detail.page.showEmptyFields")}</span>
        </label>
      }
    >
      {mappedAttributes.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2">
          {mappedAttributes.map((attribute) => (
            <div
              key={`${attribute.logicalField}-${attribute.adAttribute}`}
              className="rounded-md border bg-muted/10 px-3 py-2"
            >
              <p className="text-sm font-medium">{attribute.displayName}</p>
              <p className="mt-1 max-h-32 overflow-y-auto break-all text-sm">
                {formatMappedAdUserAttributeValue(attribute)}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">-</p>
      )}
    </SectionCard>
  );
}
