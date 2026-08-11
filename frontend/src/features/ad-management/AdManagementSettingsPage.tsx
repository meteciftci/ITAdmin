import { PageHeader } from "@/components/common/PageHeader";
import { PageContainer } from "@/components/common/PageContainer";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
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
    <PageContainer variant="wide">
      <PageHeader
        title={t("settings:pages.adManagement.title")}
        description={t("settings:pages.adManagement.description")}
      />

      <Alert>
        <AlertTitle>{t("settings:adManagement.role.title")}</AlertTitle>
        <AlertDescription>{t("settings:adManagement.role.description")}</AlertDescription>
      </Alert>

      {!canUpdateAdManagementSettings ? <Alert><AlertDescription>{t("settings:adManagement.readOnlyNotice")}</AlertDescription></Alert> : null}
      <AdManagementSettingsTab readOnly={!canUpdateAdManagementSettings} />
    </PageContainer>
  );
}
