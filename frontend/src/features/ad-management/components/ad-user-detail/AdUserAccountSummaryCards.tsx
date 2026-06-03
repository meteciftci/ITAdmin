import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { AdAccountStatusBadge } from "@/features/ad-management/components/AdAccountStatusBadge";
import { AdLockStatusBadge } from "@/features/ad-management/components/AdLockStatusBadge";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
};

export function AdUserAccountSummaryCards({ user }: Props) {
  const { t } = useTranslation("adManagement");

  const cards = useMemo(() => {
    const items = [
      {
        key: "accountStatus",
        label: t("users.detail.page.accountStatusCard"),
        content: <AdAccountStatusBadge isEnabled={user.isEnabled} />,
      },
      {
        key: "lockStatus",
        label: t("users.detail.page.lockStatusCard"),
        content: <AdLockStatusBadge isLockedOut={user.isLockedOut} />,
      },
      {
        key: "lastLogon",
        label: t("users.detail.lastLogon"),
        content: <DateTimeText value={user.lastLogonAt} />,
      },
      {
        key: "passwordLastSet",
        label: t("users.detail.passwordLastSet"),
        content: <DateTimeText value={user.passwordLastSetAt} />,
      },
    ];

    if (user.accountExpiresAt) {
      items.push({
        key: "accountExpires",
        label: t("users.detail.page.accountExpiresCard"),
        content: <DateTimeText value={user.accountExpiresAt} />,
      });
    }

    if (user.badPwdCount !== null && user.badPwdCount !== undefined) {
      items.push({
        key: "badPwdCount",
        label: t("users.detail.page.badPwdCount"),
        content: <span>{user.badPwdCount}</span>,
      });
    }

    return items;
  }, [t, user]);

  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => (
        <div
          key={card.key}
          className="rounded-lg border bg-card px-4 py-3 shadow-sm"
        >
          <p className="text-xs font-medium text-muted-foreground">{card.label}</p>
          <div className="mt-2 text-sm">{card.content}</div>
        </div>
      ))}
    </div>
  );
}
