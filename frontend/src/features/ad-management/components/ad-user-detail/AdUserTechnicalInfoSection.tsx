import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { SectionCard } from "@/components/common/SectionCard";
import { AdAccountStatusBadge } from "@/features/ad-management/components/AdAccountStatusBadge";
import { AdLockStatusBadge } from "@/features/ad-management/components/AdLockStatusBadge";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
};

export function AdUserTechnicalInfoSection({ user }: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <SectionCard title={t("users.detail.page.technicalInfo")}>
      <div className="grid gap-3 md:grid-cols-2">
        <AdUserDetailField
          label={t("users.detail.page.objectGuid")}
          value={user.id}
          valueClassName="break-all font-mono text-xs"
        />
        <AdUserDetailField
          label={t("users.detail.page.distinguishedName")}
          value={user.distinguishedName}
          valueClassName="break-all font-mono text-xs"
        />
        <AdUserDetailField label={t("users.detail.created")}>
          <DateTimeText value={user.whenCreated} />
        </AdUserDetailField>
        <AdUserDetailField label={t("users.detail.changed")}>
          <DateTimeText value={user.whenChanged} />
        </AdUserDetailField>
        <AdUserDetailField label={t("users.detail.status")}>
          <AdAccountStatusBadge isEnabled={user.isEnabled} />
        </AdUserDetailField>
        <AdUserDetailField label={t("users.detail.locked")}>
          <AdLockStatusBadge isLockedOut={user.isLockedOut} />
        </AdUserDetailField>
      </div>
    </SectionCard>
  );
}
