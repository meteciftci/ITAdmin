import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmailProviderSettingsTab } from "@/features/notification-providers/components/EmailProviderSettingsTab";
import { SmsProviderSettingsTab } from "@/features/notification-providers/components/SmsProviderSettingsTab";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";

export function NotificationProvidersPage() {
  const { t } = useTranslation(["notificationProviders"]);
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, "NotificationProviders.Update");

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("notificationProviders:title")}
        description={t("notificationProviders:description")}
      />

      {!canUpdate ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("notificationProviders:readOnlyNotice")}
        </p>
      ) : null}

      <SectionCard>
        <Tabs defaultValue="sms">
          <TabsList>
            <TabsTrigger value="sms">{t("notificationProviders:tabs.sms")}</TabsTrigger>
            <TabsTrigger value="email">{t("notificationProviders:tabs.email")}</TabsTrigger>
          </TabsList>
          <TabsContent value="sms" className="mt-4">
            <SmsProviderSettingsTab readOnly={!canUpdate} />
          </TabsContent>
          <TabsContent value="email" className="mt-4">
            <EmailProviderSettingsTab readOnly={!canUpdate} />
          </TabsContent>
        </Tabs>
      </SectionCard>
    </section>
  );
}
