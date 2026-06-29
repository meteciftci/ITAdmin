import type { AdUserListItem } from "@/features/ad-management/types";
import { AdUserSearchCombobox } from "@/features/ad-management/components/AdUserSearchCombobox";
import { mapAdUserToSnapshot } from "@/features/license-management/license-request-payload";
import type { LicenseRequestAdUserSnapshot } from "@/features/license-management/types";

type Props = {
  value: LicenseRequestAdUserSnapshot | null;
  onChange: (value: LicenseRequestAdUserSnapshot | null) => void;
  disabled?: boolean;
  label?: string;
  placeholder?: string;
};

function toAdUserListItem(snapshot: LicenseRequestAdUserSnapshot): AdUserListItem {
  return {
    id: snapshot.adObjectId,
    distinguishedName: "",
    samAccountName: snapshot.samAccountName ?? null,
    userPrincipalName: snapshot.userPrincipalName ?? null,
    displayName: snapshot.displayName ?? null,
    mail: snapshot.mail ?? null,
    department: snapshot.department ?? null,
    isEnabled: true,
    isLockedOut: false,
    whenCreated: null,
    whenChanged: null,
    lastLogonAt: null,
  };
}

export function LicenseAdUserPicker({
  value,
  onChange,
  disabled,
  label,
  placeholder,
}: Props) {
  return (
    <AdUserSearchCombobox
      value={value ? toAdUserListItem(value) : null}
      onChange={(user) => onChange(user ? mapAdUserToSnapshot(user) : null)}
      disabled={disabled}
      label={label}
      placeholder={placeholder}
    />
  );
}
