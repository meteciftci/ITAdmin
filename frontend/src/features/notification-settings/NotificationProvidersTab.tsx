import { useTranslation } from "react-i18next";

import { SectionCard } from "@/components/common/SectionCard";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmailProviderSettingsTab } from "@/features/notification-providers/components/EmailProviderSettingsTab";
import { SmsProviderSettingsTab } from "@/features/notification-providers/components/SmsProviderSettingsTab";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";

export function NotificationProvidersTab() {
  const { t } = useTranslation(["notificationProviders", "notificationSettings", "common"]);
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, "NotificationProviders.Update");

  return (
    <div className="space-y-4">
      {!canUpdate ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("notificationProviders:readOnlyNotice")}
        </p>
      ) : null}

      <SectionCard>
        <Tabs defaultValue="sms">
          <TabsList>
            <TabsTrigger value="sms">{t("common:channels.sms")}</TabsTrigger>
            <TabsTrigger value="email">{t("common:channels.email")}</TabsTrigger>
          </TabsList>
          <TabsContent value="sms" className="mt-4">
            <SmsProviderSettingsTab readOnly={!canUpdate} />
          </TabsContent>
          <TabsContent value="email" className="mt-4">
            <EmailProviderSettingsTab readOnly={!canUpdate} />
          </TabsContent>
        </Tabs>
      </SectionCard>
    </div>
  );
}
