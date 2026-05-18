import type { ReactNode } from "react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { DetailDialog } from "@/components/common/DetailDialog";
import { DateTimeText } from "@/components/common/DateTimeText";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { AdAccountStatusBadge } from "@/features/ad-management/components/AdAccountStatusBadge";
import { AdLockStatusBadge } from "@/features/ad-management/components/AdLockStatusBadge";
import type { AdUserDetail, MappedAdUserAttribute } from "@/features/ad-management/types";

const MAX_ATTRIBUTE_VALUE_LENGTH = 500;

type Props = {
  user: AdUserDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

function formatAttributeValues(values: string[]): string {
  const joined = values.join(", ");
  if (joined.length <= MAX_ATTRIBUTE_VALUE_LENGTH) {
    return joined;
  }

  return `${joined.slice(0, MAX_ATTRIBUTE_VALUE_LENGTH)}…`;
}

function formatMappedValue(attribute: MappedAdUserAttribute): string {
  if (!attribute.value?.length) {
    return "-";
  }

  return formatAttributeValues(attribute.value);
}

export function AdUserDetailDialog({ user, open, onOpenChange }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);

  const mappedAttributes = useMemo(
    () =>
      user?.mappedAttributes
        .filter((item) => item.value?.some((value) => value.trim().length > 0))
        .sort((left, right) => left.sortOrder - right.sortOrder) ?? [],
    [user],
  );

  return (
    <DetailDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("adManagement:users.detail.dialogTitle")}
      description={user?.displayName ?? user?.samAccountName ?? undefined}
    >
      {user ? (
        <div className="space-y-4 text-sm">
          <section className="space-y-2">
            <SectionTitle>{t("adManagement:users.detail.sections.basic")}</SectionTitle>
            <div className="grid gap-3 md:grid-cols-2">
              <DetailField label={t("adManagement:users.detail.displayName")} value={user.displayName} />
              <DetailField label={t("adManagement:users.detail.username")} value={user.samAccountName} />
              <DetailField label={t("adManagement:users.detail.upn")} value={user.userPrincipalName} />
              <DetailField label={t("adManagement:users.detail.email")} value={user.mail} />
              <DetailField label={t("adManagement:users.detail.givenName")} value={user.givenName} />
              <DetailField label={t("adManagement:users.detail.surname")} value={user.surname} />
              <DetailField label={t("adManagement:users.detail.department")} value={user.department} />
            </div>
          </section>

          <Separator />

          {mappedAttributes.length > 0 ? (
            <>
              <section className="space-y-2">
                <SectionTitle>{t("adManagement:users.detail.mappedAttributes")}</SectionTitle>
                <div className="grid gap-3 md:grid-cols-2">
                  {mappedAttributes.map((attribute) => (
                    <div
                      key={`${attribute.logicalField}-${attribute.adAttribute}`}
                      className="rounded-md border bg-muted/10 px-3 py-2"
                    >
                      <p className="text-sm font-medium">{attribute.displayName}</p>
                      <p className="mt-1 break-all text-sm">{formatMappedValue(attribute)}</p>
                    </div>
                  ))}
                </div>
              </section>
              <Separator />
            </>
          ) : null}

          <section className="space-y-2">
            <SectionTitle>{t("adManagement:users.detail.sections.accountStatus")}</SectionTitle>
            <div className="grid gap-3 md:grid-cols-2">
              <DetailField label={t("adManagement:users.detail.status")}>
                <AdAccountStatusBadge isEnabled={user.isEnabled} />
              </DetailField>
              <DetailField label={t("adManagement:users.detail.locked")}>
                <AdLockStatusBadge isLockedOut={user.isLockedOut} />
              </DetailField>
              <DetailField label={t("adManagement:users.detail.created")}>
                <DateTimeText value={user.whenCreated} />
              </DetailField>
              <DetailField label={t("adManagement:users.detail.changed")}>
                <DateTimeText value={user.whenChanged} />
              </DetailField>
              <DetailField label={t("adManagement:users.detail.lastLogon")}>
                <DateTimeText value={user.lastLogonAt} />
              </DetailField>
              <DetailField label={t("adManagement:users.detail.passwordLastSet")}>
                <DateTimeText value={user.passwordLastSetAt} />
              </DetailField>
            </div>
          </section>

          <Separator />

          <section className="space-y-2">
            <SectionTitle>{t("adManagement:users.detail.groups")}</SectionTitle>
            {user.groups.length > 0 ? (
              <div className="flex max-h-40 flex-wrap gap-2 overflow-y-auto rounded-md border bg-muted/20 p-2">
                {user.groups.map((group) => (
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
          </section>
        </div>
      ) : null}
    </DetailDialog>
  );
}

function SectionTitle({ children }: { children: ReactNode }) {
  return <p className="text-xs font-medium text-muted-foreground">{children}</p>;
}

function DetailField({
  label,
  value,
  children,
}: {
  label: string;
  value?: string | null;
  children?: ReactNode;
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      {children ?? <p>{value || "-"}</p>}
    </div>
  );
}
