import { useState } from "react";
import { useTranslation } from "react-i18next";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmailProviderSettingsTab } from "@/features/notification-providers/components/EmailProviderSettingsTab";
import { SmsProviderSettingsTab } from "@/features/notification-providers/components/SmsProviderSettingsTab";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

export function NotificationProvidersTab() {
  const { t } = useTranslation(["notificationProviders", "notificationSettings", "common"]);
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, PermissionCodes.NotificationProviders.Update);
  const [activeProvider, setActiveProvider] = useState("sms");
  const [isDirty, setIsDirty] = useState(false);
  const [pendingProvider, setPendingProvider] = useState<string | null>(null);

  const changeProvider = (nextProvider: string) => {
    if (nextProvider === activeProvider) return;
    if (isDirty) {
      setPendingProvider(nextProvider);
      return;
    }
    setActiveProvider(nextProvider);
  };

  return (
    <div className="space-y-4">
      {!canUpdate ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("notificationProviders:readOnlyNotice")}
        </p>
      ) : null}

      <Tabs value={activeProvider} onValueChange={changeProvider}>
          <TabsList>
            <TabsTrigger value="sms">{t("common:channels.sms")}</TabsTrigger>
            <TabsTrigger value="email">{t("common:channels.email")}</TabsTrigger>
          </TabsList>
          <TabsContent value="sms" className="mt-4">
            <SmsProviderSettingsTab readOnly={!canUpdate} onDirtyChange={setIsDirty} />
          </TabsContent>
          <TabsContent value="email" className="mt-4">
            <EmailProviderSettingsTab readOnly={!canUpdate} onDirtyChange={setIsDirty} />
          </TabsContent>
        </Tabs>

      <ConfirmDialog
        open={pendingProvider !== null}
        title={t("notificationProviders:unsaved.title")}
        description={t("notificationProviders:unsaved.description")}
        confirmText={t("notificationProviders:unsaved.leave")}
        cancelText={t("notificationProviders:unsaved.stay")}
        variant="danger"
        onConfirm={() => {
          if (pendingProvider) setActiveProvider(pendingProvider);
          setPendingProvider(null);
        }}
        onOpenChange={(open) => {
          if (!open) setPendingProvider(null);
        }}
      />
    </div>
  );
}
