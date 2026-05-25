import { Link } from "react-router-dom";
import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DataTable } from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { createNotificationTemplateColumns } from "@/features/notification-templates/notification-template-columns";
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

  const items = listQuery.data ?? [];

  const columns = useMemo(
    () =>
      createNotificationTemplateColumns({
        t,
        canUpdate,
        useCatalogLabels: true,
        editHref: (item) => `/settings/notifications/templates/${item.id}/edit`,
        editLabel: t("notificationSettings:templates.actions.edit"),
      }),
    [t, canUpdate],
  );

  const table = useClientDataTable({
    data: items,
    columns,
    enablePagination: false,
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

        {!listQuery.isLoading && items.length > 0 ? <DataTable table={table} /> : null}
      </div>
    </SectionCard>
  );
}
