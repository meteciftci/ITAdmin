import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
};

export function AdUserBasicInfoSection({ user }: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <SectionCard title={t("users.detail.page.basicInfo")}>
      <div className="grid gap-3 md:grid-cols-2">
        <AdUserDetailField
          label={t("users.detail.displayName")}
          value={user.displayName}
        />
        <AdUserDetailField label={t("users.detail.givenName")} value={user.givenName} />
        <AdUserDetailField label={t("users.detail.surname")} value={user.surname} />
        <AdUserDetailField label={t("users.detail.username")} value={user.samAccountName} />
        <AdUserDetailField
          label={t("users.detail.upn")}
          value={user.userPrincipalName}
          valueClassName="break-all"
        />
        <AdUserDetailField
          label={t("users.detail.email")}
          value={user.mail}
          valueClassName="break-all"
        />
        <AdUserDetailField label={t("users.detail.department")} value={user.department} />
      </div>
    </SectionCard>
  );
}
