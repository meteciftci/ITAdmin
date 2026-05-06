import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DataToolbar } from "@/components/common/DataToolbar";
import { CodeBadge } from "@/components/common/CodeBadge";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { StatusBadge } from "@/components/common/StatusBadge";
import { TablePagination } from "@/components/common/TablePagination";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { getPermissions } from "@/features/permissions/api";
import { getApiErrorMessage } from "@/lib/api-error";

type StatusFilter = "active" | "passive" | "all";

const getGroupValue = (permission: {
  group?: string | null;
  module?: string | null;
  category?: string | null;
}): string => permission.group ?? permission.module ?? permission.category ?? "";

export function PermissionsPage() {
  const { t } = useTranslation(["permissions", "common"]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const permissionsQuery = useQuery({
    queryKey: ["permissions", "list", search, statusFilter, pageNumber, pageSize],
    queryFn: () =>
      getPermissions({
        search: search.trim() || undefined,
        isActive:
          statusFilter === "all"
            ? undefined
            : statusFilter === "active"
              ? true
              : false,
        pageNumber,
        pageSize,
      }),
  });

  const permissions = useMemo(
    () => permissionsQuery.data?.items ?? [],
    [permissionsQuery.data],
  );

  const showStatusColumn = permissions.some(
    (permission) => typeof permission.isActive === "boolean",
  );
  const showGroupColumn = permissions.some((permission) =>
    Boolean(getGroupValue(permission)),
  );

  const handleRefresh = () => {
    permissionsQuery.refetch();
  };
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("permissions:title")}
        description={t("permissions:description")}
      />

      <SectionCard title={t("permissions:sections.listTitle")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("permissions:search.placeholder")}
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          >
            <Select
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value as StatusFilter);
                setPageNumber(1);
              }}
              className="w-full sm:w-40"
            >
              <option value="active">{t("common:status.active")}</option>
              <option value="passive">{t("common:status.passive")}</option>
              <option value="all">{t("common:status.all")}</option>
            </Select>
          </DataToolbar>

          {permissionsQuery.isLoading ? <LoadingState /> : null}

          {permissionsQuery.isError ? (
            <ErrorState
              title={t("permissions:errors.loadFailed")}
              description={getApiErrorMessage(
                permissionsQuery.error,
                t("permissions:errors.loadFailed"),
              )}
              retry={
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
              }
            />
          ) : null}

          {permissionsQuery.isSuccess && !permissions.length ? (
            <EmptyState
              title={t("permissions:empty.title")}
              description={t("permissions:empty.description")}
            />
          ) : null}

          {permissions.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">
                      {t("permissions:table.name")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("permissions:table.code")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("permissions:table.description")}
                    </th>
                    {showGroupColumn ? (
                      <th className="px-3 py-2 font-medium">
                        {t("permissions:table.group")}
                      </th>
                    ) : null}
                    {showStatusColumn ? (
                      <th className="px-3 py-2 font-medium">
                        {t("permissions:table.status")}
                      </th>
                    ) : null}
                  </tr>
                </thead>
                <tbody>
                  {permissions.map((permission) => (
                    <tr
                      key={permission.id}
                      className="border-t align-top hover:bg-muted/20"
                    >
                      <td className="px-3 py-2">{permission.name}</td>
                      <td className="px-3 py-2">
                        <CodeBadge>{permission.code}</CodeBadge>
                      </td>
                      <td className="max-w-96 px-3 py-2">
                        <span className="line-clamp-2">
                          {permission.description || "-"}
                        </span>
                      </td>
                      {showGroupColumn ? (
                        <td className="px-3 py-2">
                          {getGroupValue(permission) || "-"}
                        </td>
                      ) : null}
                      {showStatusColumn ? (
                        <td className="px-3 py-2">
                          <StatusBadge isActive={permission.isActive} />
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
              {permissionsQuery.data && permissionsQuery.data.totalCount > 0 ? (
                <TablePagination
                  pageNumber={permissionsQuery.data.pageNumber}
                  pageSize={permissionsQuery.data.pageSize}
                  totalCount={permissionsQuery.data.totalCount}
                  totalPages={permissionsQuery.data.totalPages}
                  onPageChange={setPageNumber}
                  onPageSizeChange={(nextPageSize) => {
                    setPageSize(nextPageSize);
                    setPageNumber(1);
                  }}
                />
              ) : null}
            </div>
          ) : null}
        </div>
      </SectionCard>
    </section>
  );
}
