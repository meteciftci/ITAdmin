import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { AdManagementSettingsTab } from "@/features/ad-management/AdManagementSettingsTab";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { useTranslation } from "react-i18next";
import { PermissionCodes } from "@/lib/permission-codes";

export function AdManagementSettingsPage() {
  const { t } = useTranslation(["settings"]);
  const user = useAuthStore((state) => state.user);
  const canUpdateAdManagementSettings = canAccess(user, PermissionCodes.AdManagement.Settings.Update);

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("settings:pages.adManagement.title")}
        description={t("settings:pages.adManagement.description")}
      />

      {!canUpdateAdManagementSettings ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:adManagement.readOnlyNotice")}
        </p>
      ) : null}

      <SectionCard>
        <AdManagementSettingsTab readOnly={!canUpdateAdManagementSettings} />
      </SectionCard>
    </section>
  );
}
