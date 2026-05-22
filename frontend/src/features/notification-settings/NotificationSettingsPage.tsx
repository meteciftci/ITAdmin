import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { NotificationProvidersTab } from "@/features/notification-settings/NotificationProvidersTab";
import { NotificationSettingsTabs } from "@/features/notification-settings/NotificationSettingsTabs";
import { NotificationTemplatesListTab } from "@/features/notification-settings/NotificationTemplatesListTab";

type NotificationSettingsPageProps = {
  activeTab: "providers" | "templates";
};

export function NotificationSettingsPage({ activeTab }: NotificationSettingsPageProps) {
  const { t } = useTranslation(["notificationSettings"]);

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("notificationSettings:title")}
        description={t("notificationSettings:description")}
      />

      <NotificationSettingsTabs activeTab={activeTab} />

      {activeTab === "providers" ? <NotificationProvidersTab /> : <NotificationTemplatesListTab />}
    </section>
  );
}
