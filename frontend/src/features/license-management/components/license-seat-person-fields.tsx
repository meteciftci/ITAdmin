import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { LicenseAdUserPicker } from "@/features/license-management/components/LicenseAdUserPicker";
import { EMPTY_PERSON, type PersonDraft } from "@/features/license-management/components/license-seat-person";

/**
 * Person picker for a licence assignment. When the AD Management module is
 * operational it offers an AD user search that hard-links the assignment to the
 * directory object id; otherwise (or as a fallback) name + e-mail are entered by
 * hand. No national id - assignments are keyed off directory identity.
 */
export function PersonFields({
  draft,
  onChange,
}: {
  draft: PersonDraft;
  onChange: (next: PersonDraft) => void;
}) {
  const { t } = useTranslation(["licenseManagement"]);
  const adStatus = useAdManagementModuleStatus();

  return (
    <div className="space-y-3">
      {adStatus.isOperational ? (
        <LicenseAdUserPicker
          label={t("licenseManagement:seats.dialog.adUser")}
          searchPlaceholder={t("licenseManagement:seats.dialog.adSearchPlaceholder")}
          value={
            draft.adObjectId
              ? {
                  adObjectId: draft.adObjectId,
                  samAccountName: draft.samAccountName,
                  userPrincipalName: draft.userPrincipalName,
                  displayName: draft.displayName,
                  department: draft.department,
                  title: draft.title,
                  mail: draft.mail,
                  phone: null,
                }
              : null
          }
          onChange={(snapshot) =>
            onChange(
              snapshot
                ? {
                    adObjectId: snapshot.adObjectId,
                    displayName: snapshot.displayName ?? draft.displayName,
                    samAccountName: snapshot.samAccountName ?? null,
                    userPrincipalName: snapshot.userPrincipalName ?? null,
                    mail: snapshot.mail ?? null,
                    department: snapshot.department ?? null,
                    title: snapshot.title ?? null,
                  }
                : EMPTY_PERSON,
            )
          }
        />
      ) : (
        <p className="text-xs text-muted-foreground">{t("licenseManagement:seats.dialog.adUserHint")}</p>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <Label htmlFor="seat-display-name">{t("licenseManagement:seats.dialog.displayName")}</Label>
          <Input
            id="seat-display-name"
            value={draft.displayName}
            onChange={(event) => onChange({ ...draft, displayName: event.target.value })}
          />
        </div>
        <div className="space-y-1">
          <Label htmlFor="seat-mail">{t("licenseManagement:seats.dialog.mail")}</Label>
          <Input
            id="seat-mail"
            type="email"
            value={draft.mail ?? ""}
            onChange={(event) => onChange({ ...draft, mail: event.target.value || null })}
          />
        </div>
      </div>
    </div>
  );
}
