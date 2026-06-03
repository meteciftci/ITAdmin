import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import {
  formatAdUserAccountControlValue,
  parseAdUserAccountControlFlags,
} from "@/features/ad-management/ad-user-account-control";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import type { AdUserDetail } from "@/features/ad-management/types";

type Props = {
  user: AdUserDetail;
};

export function AdUserTechnicalInfoSection({ user }: Props) {
  const { t } = useTranslation("adManagement");

  const accountControl = useMemo(
    () => parseAdUserAccountControlFlags(user.userAccountControl),
    [user.userAccountControl],
  );

  return (
    <SectionCard title={t("users.detail.page.technicalInfo")}>
      <div className="space-y-4">
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
          <AdUserDetailField label={t("users.detail.lastLogon")}>
            <DateTimeText value={user.lastLogonAt} />
          </AdUserDetailField>
          <AdUserDetailField label={t("users.detail.page.lastLogonTimestamp")}>
            <DateTimeText value={user.lastLogonTimestampAt} />
          </AdUserDetailField>
          <AdUserDetailField label={t("users.detail.passwordLastSet")}>
            <DateTimeText value={user.passwordLastSetAt} />
          </AdUserDetailField>
          <AdUserDetailField label={t("users.detail.page.accountExpires")}>
            <DateTimeText value={user.accountExpiresAt} />
          </AdUserDetailField>
          <AdUserDetailField label={t("users.detail.page.lockoutTime")}>
            <DateTimeText value={user.lockoutTimeAt} />
          </AdUserDetailField>
          <AdUserDetailField
            label={t("users.detail.page.badPwdCount")}
            value={
              user.badPwdCount === null || user.badPwdCount === undefined
                ? null
                : String(user.badPwdCount)
            }
          />
          <AdUserDetailField label={t("users.detail.page.badPasswordTime")}>
            <DateTimeText value={user.badPasswordTimeAt} />
          </AdUserDetailField>
          <AdUserDetailField
            label={t("users.detail.page.userAccountControl")}
            value={formatAdUserAccountControlValue(user.userAccountControl)}
            valueClassName="font-mono text-xs"
          />
        </div>

        <div className="space-y-2">
          <p className="text-xs font-medium text-muted-foreground">
            {t("users.detail.page.userAccountControlFlags")}
          </p>
          {accountControl.flags.length > 0 || accountControl.unknownMask > 0 ? (
            <div className="flex max-h-32 flex-wrap gap-2 overflow-y-auto rounded-md border bg-muted/10 p-2">
              {accountControl.flags.map((flag) => (
                <Badge key={flag} variant="secondary" className="font-mono text-xs">
                  {t(`users.detail.page.uacFlags.${flag}`)}
                </Badge>
              ))}
              {accountControl.unknownMask > 0 ? (
                <Badge variant="outline" className="font-mono text-xs">
                  {t("users.detail.page.uacUnknownFlags", {
                    value: `0x${accountControl.unknownMask.toString(16).toUpperCase()}`,
                  })}
                </Badge>
              ) : null}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">-</p>
          )}
        </div>
      </div>
    </SectionCard>
  );
}
