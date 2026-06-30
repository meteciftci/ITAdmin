import { X } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { AdUserSearchCombobox } from "@/features/ad-management/components/AdUserSearchCombobox";
import { mapAdUserToSnapshot } from "@/features/license-management/license-request-payload";
import type { LicenseRequestAdUserSnapshot } from "@/features/license-management/types";

type Props = {
  users: LicenseRequestAdUserSnapshot[];
  onChange: (users: LicenseRequestAdUserSnapshot[]) => void;
  disabled?: boolean;
  label?: string;
  placeholder?: string;
  searchPlaceholder?: string;
};

function formatUserLabel(user: LicenseRequestAdUserSnapshot): string {
  return user.displayName || user.samAccountName || user.userPrincipalName || user.adObjectId;
}

export function LicenseAdUserMultiSelect({
  users,
  onChange,
  disabled,
  label,
  placeholder,
  searchPlaceholder,
}: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  const selectedIds = new Set(users.map((user) => user.adObjectId));

  return (
    <div className="space-y-3">
      <AdUserSearchCombobox
        value={null}
        onChange={(user) => {
          if (!user || selectedIds.has(user.id)) {
            return;
          }

          onChange([...users, mapAdUserToSnapshot(user)]);
        }}
        disabled={disabled}
        label={label}
        placeholder={placeholder}
        searchPlaceholder={searchPlaceholder}
      />

      {users.length > 0 ? (
        <div className="space-y-2">
          <p className="text-sm font-medium text-foreground">
            {t("licenseManagement:requests.fields.selectedUsers")}
          </p>
          <ul className="flex flex-wrap gap-2">
            {users.map((user) => (
              <li
                key={user.adObjectId}
                className="inline-flex items-center gap-1 rounded-md border bg-muted/40 px-2 py-1 text-sm"
              >
                <span title={user.userPrincipalName ?? undefined}>{formatUserLabel(user)}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  disabled={disabled}
                  aria-label={t("common:actions.remove")}
                  onClick={() => onChange(users.filter((item) => item.adObjectId !== user.adObjectId))}
                >
                  <X className="h-3.5 w-3.5" />
                </Button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
