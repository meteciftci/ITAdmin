import { useMemo } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button-variants";
import { buildAdUserGroupsSummary } from "@/features/ad-management/ad-user-detail-utils";
import { cn } from "@/lib/utils";
import type { AdUserDetail } from "@/features/ad-management/types";

const manageGroupsButtonClass = cn(
  buttonVariants({ size: "sm" }),
  "inline-flex h-8 min-h-8 items-center justify-center px-3 text-sm",
  "border border-emerald-500/30 bg-emerald-500/15 text-emerald-700 hover:bg-emerald-500/25",
  "dark:bg-emerald-500/15 dark:text-emerald-300 dark:hover:bg-emerald-500/25",
);

type Props = {
  user: AdUserDetail;
  canManageGroups: boolean;
};

export function AdUserGroupsSummarySection({ user, canManageGroups }: Props) {
  const { t } = useTranslation("adManagement");

  const summary = useMemo(() => buildAdUserGroupsSummary(user.groups), [user.groups]);

  return (
    <SectionCard
      title={t("users.detail.page.groupsSummary")}
      actions={
        canManageGroups ? (
          <Link
            to={`/ad-management/users/${user.id}/groups`}
            className={manageGroupsButtonClass}
          >
            {t("users.actions.manageGroups")}
          </Link>
        ) : null
      }
    >
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
                title={group.name}
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
