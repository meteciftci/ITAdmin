import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Select } from "@/components/ui/select";
import {
  filterMappedAttributesForDisplay,
  formatMappedAdUserAttributeValue,
  type MappedAttributeDisplayFilter,
} from "@/features/ad-management/ad-user-detail-utils";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
  filter: MappedAttributeDisplayFilter;
  onFilterChange: (value: MappedAttributeDisplayFilter) => void;
};

export function AdUserMappedAttributesSection({
  user,
  filter,
  onFilterChange,
}: Props) {
  const { t } = useTranslation("adManagement");

  const mappedAttributes = useMemo(
    () => filterMappedAttributesForDisplay(user.mappedAttributes, filter),
    [filter, user.mappedAttributes],
  );

  return (
    <SectionCard
      title={t("users.detail.page.mappedAttributes")}
      actions={
        <Select
          value={filter}
          onChange={(event) => {
            onFilterChange(event.target.value as MappedAttributeDisplayFilter);
          }}
          className="h-8 min-w-[8rem] text-sm"
          aria-label={t("users.detail.page.mappedAttributesFilter")}
        >
          <option value="filled">{t("users.detail.page.mappedFilter.filled")}</option>
          <option value="empty">{t("users.detail.page.mappedFilter.empty")}</option>
          <option value="all">{t("users.detail.page.mappedFilter.all")}</option>
        </Select>
      }
    >
      {mappedAttributes.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2">
          {mappedAttributes.map((attribute) => (
            <div
              key={`${attribute.logicalField}-${attribute.adAttribute}`}
              className="rounded-md border bg-muted/10 px-3 py-2"
            >
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm font-medium">{attribute.displayName}</p>
                {attribute.isSensitive ? (
                  <Badge variant="outline" className="text-xs">
                    {t("users.detail.page.sensitiveBadge")}
                  </Badge>
                ) : null}
                {attribute.isEditable ? (
                  <Badge variant="secondary" className="text-xs">
                    {t("users.detail.page.editableBadge")}
                  </Badge>
                ) : null}
              </div>
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
