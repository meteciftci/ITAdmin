import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { NotificationTemplateStatusSwitch } from "@/features/notification-settings/NotificationTemplateStatusSwitch";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  getCatalogEventLabel,
  getCatalogModuleLabel,
  getChannelLabel,
} from "@/features/notification-settings/catalog-labels";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  getNotificationTemplates,
} from "@/features/notification-templates/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

export function NotificationTemplatesListTab() {
  const { t } = useTranslation(["notificationSettings", "notificationTemplates", "common"]);
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, "NotificationTemplates.Update");

  const listQuery = useQuery({
    queryKey: NOTIFICATION_TEMPLATES_QUERY_KEY,
    queryFn: () => getNotificationTemplates(),
  });

  return (
    <SectionCard title={t("notificationTemplates:sections.list")}>
      <div className="space-y-4">
        {canUpdate ? (
          <div className="flex justify-end">
            <Link
              to="/settings/notifications/templates/create"
              className={cn(buttonVariants({ variant: "default" }))}
            >
              {t("notificationSettings:templates.actions.add")}
            </Link>
          </div>
        ) : null}

        {listQuery.isLoading ? <LoadingState /> : null}
        {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
          <EmptyState title={t("notificationTemplates:empty.title")} />
        ) : null}

        {!listQuery.isLoading && (listQuery.data?.length ?? 0) > 0 ? (
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-muted/40 text-left">
                <tr>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.module")}</th>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.event")}</th>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.channel")}</th>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.name")}</th>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.status")}</th>
                  <th className="px-3 py-2">{t("notificationTemplates:columns.updatedAt")}</th>
                  {canUpdate ? (
                    <th className="px-3 py-2">{t("notificationTemplates:columns.actions")}</th>
                  ) : null}
                </tr>
              </thead>
              <tbody>
                {listQuery.data?.map((item) => (
                  <tr key={item.id} className="border-t">
                    <td className="px-3 py-2">
                      {getCatalogModuleLabel(t, item.moduleKey)}
                    </td>
                    <td className="px-3 py-2">
                      {getCatalogEventLabel(t, item.moduleKey, item.eventKey)}
                    </td>
                    <td className="px-3 py-2">{getChannelLabel(t, item.channel)}</td>
                    <td className="px-3 py-2">{item.name}</td>
                    <td className="px-3 py-2">
                      <div className="flex items-center gap-2">
                        <NotificationTemplateStatusSwitch
                          key={`${item.id}-${item.isEnabled}`}
                          templateId={item.id}
                          isEnabled={item.isEnabled}
                          canUpdate={canUpdate}
                        />
                        {!canUpdate ? <StatusBadge isActive={item.isEnabled} /> : null}
                      </div>
                    </td>
                    <td className="px-3 py-2">
                      {item.updatedAt ? <DateTimeText value={item.updatedAt} /> : "-"}
                    </td>
                    {canUpdate ? (
                      <td className="px-3 py-2">
                        <Link
                          to={`/settings/notifications/templates/${item.id}/edit`}
                          className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
                        >
                          {t("notificationSettings:templates.actions.edit")}
                        </Link>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </div>
    </SectionCard>
  );
}
