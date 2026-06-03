import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { buildAdUserGroupsSummary } from "@/features/ad-management/ad-user-detail-utils";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
};

export function AdUserGroupsSummarySection({ user }: Props) {
  const { t } = useTranslation("adManagement");

  const summary = useMemo(() => buildAdUserGroupsSummary(user.groups), [user.groups]);

  return (
    <SectionCard title={t("users.detail.page.groupsSummary")}>
      <div className="space-y-3">
        <p className="text-sm text-muted-foreground">
          {t("users.detail.page.totalGroups", { count: summary.totalCount })}
        </p>
        {summary.previewGroups.length > 0 ? (
          <div className="flex max-h-40 flex-wrap gap-2 overflow-y-auto rounded-md border bg-muted/20 p-2">
            {summary.previewGroups.map((group) => (
              <Badge
                key={group.distinguishedName}
                variant="secondary"
                className="max-w-full whitespace-normal break-words"
                title={group.distinguishedName}
              >
                {group.name}
              </Badge>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">-</p>
        )}
        {summary.remainingCount > 0 ? (
          <p className="text-sm text-muted-foreground">
            {t("users.detail.page.moreGroups", { count: summary.remainingCount })}
          </p>
        ) : null}
      </div>
    </SectionCard>
  );
}
