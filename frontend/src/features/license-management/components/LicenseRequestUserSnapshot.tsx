import { useTranslation } from "react-i18next";

import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import type { LicenseRequestAdUserSnapshot } from "@/features/license-management/types";

type Props = {
  snapshot: LicenseRequestAdUserSnapshot;
  title?: string;
};

export function LicenseRequestUserSnapshot({ snapshot, title }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);

  return (
    <div className="grid gap-3 md:grid-cols-2">
      <LicenseDetailField label={t("licenseManagement:requests.fields.displayName")}>
        {snapshot.displayName ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.samAccountName")}>
        {snapshot.samAccountName ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.userPrincipalName")}>
        {snapshot.userPrincipalName ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.department")}>
        {snapshot.department ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.title")}>
        {snapshot.title ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.mail")}>
        {snapshot.mail ?? "-"}
      </LicenseDetailField>
      <LicenseDetailField label={t("licenseManagement:requests.fields.phone")}>
        {snapshot.phone ?? "-"}
      </LicenseDetailField>
      {title ? (
        <LicenseDetailField label={t("licenseManagement:requests.fields.snapshotTitle")}>
          {title}
        </LicenseDetailField>
      ) : null}
    </div>
  );
}
