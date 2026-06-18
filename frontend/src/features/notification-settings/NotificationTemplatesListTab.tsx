import { Link } from "react-router-dom";
import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
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
import {
  getCatalogEventLabel,
  getCatalogModuleLabel,
  getChannelLabel,
} from "@/features/notification-settings/catalog-labels";
import type { NotificationTemplateListItem } from "@/features/notification-templates/types";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { PermissionCodes } from "@/lib/permission-codes";

const PAGE_SIZE_OPTIONS = [10, 25, 50];
const DEFAULT_PAGE_SIZE = 10;

export function NotificationTemplatesListTab() {
  const { t } = useTranslation(["notificationSettings", "notificationTemplates", "common"]);
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, PermissionCodes.NotificationTemplates.Update);

  const [search, setSearch] = useState("");

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

  const getSearchableValue = useMemo(
    () => (row: NotificationTemplateListItem) =>
      [
        row.moduleKey,
        getCatalogModuleLabel(t, row.moduleKey),
        row.eventKey,
        getCatalogEventLabel(t, row.moduleKey, row.eventKey),
        row.channel,
        getChannelLabel(t, row.channel),
        row.name,
      ]
        .filter(Boolean)
        .join(" "),
    [t],
  );

  const table = useClientDataTable({
    data: items,
    columns,
    globalFilter: search,
    enableGlobalFilter: true,
    getSearchableValue,
    initialPageSize: DEFAULT_PAGE_SIZE,
  });

  const hasRows = items.length > 0;

  return (
    <SectionCard title={t("notificationTemplates:sections.list")}>
      <div className="space-y-4">
        <DataTableToolbar
          searchValue={search}
          onSearchChange={setSearch}
          searchPlaceholder={t("notificationSettings:templates.searchPlaceholder")}
          actions={
            canUpdate ? (
              <Link
                to="/settings/notifications/templates/create"
                className={cn(buttonVariants({ variant: "default" }))}
              >
                {t("notificationSettings:templates.actions.add")}
              </Link>
            ) : null
          }
        />

        {listQuery.isLoading ? <LoadingState /> : null}
        {!listQuery.isLoading && items.length === 0 ? (
          <EmptyState title={t("notificationTemplates:empty.title")} />
        ) : null}

        {!listQuery.isLoading && items.length > 0 ? (
          <DataTable
            table={table}
            emptyMessage={t("common:dataTable.noResults")}
            footer={
              hasRows ? (
                <DataTablePagination
                  mode="client"
                  table={table}
                  pageSizeOptions={PAGE_SIZE_OPTIONS}
                />
              ) : undefined
            }
          />
        ) : null}
      </div>
    </SectionCard>
  );
}
