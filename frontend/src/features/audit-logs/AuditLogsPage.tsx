import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DataToolbar } from "@/components/common/DataToolbar";
import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { getAuditLogs } from "@/features/audit-logs/api";
import { getApiErrorMessage } from "@/lib/api-error";

export function AuditLogsPage() {
  const { t } = useTranslation(["auditLogs", "common"]);

  const [search, setSearch] = useState("");
  const [action, setAction] = useState("");
  const [entityName, setEntityName] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const auditLogsQuery = useQuery({
    queryKey: ["audit-logs", "list", search, action, entityName, from, to],
    queryFn: () =>
      getAuditLogs({
        search: search.trim() || undefined,
        action: action.trim() || undefined,
        entityName: entityName.trim() || undefined,
        from: from || undefined,
        to: to || undefined,
        pageNumber: 1,
        pageSize: 50,
      }),
  });

  const auditLogs = useMemo(() => auditLogsQuery.data?.items ?? [], [auditLogsQuery.data]);

  const handleRefresh = () => {
    auditLogsQuery.refetch();
  };

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("auditLogs:title")}
        description={t("auditLogs:description")}
      />

      <SectionCard title={t("auditLogs:sections.listTitle")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={setSearch}
            searchPlaceholder={t("auditLogs:filters.searchPlaceholder")}
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          >
            <Input
              value={action}
              onChange={(event) => setAction(event.target.value)}
              placeholder={t("auditLogs:filters.actionPlaceholder")}
              className="w-full sm:w-44"
            />
            <Input
              value={entityName}
              onChange={(event) => setEntityName(event.target.value)}
              placeholder={t("auditLogs:filters.entityNamePlaceholder")}
              className="w-full sm:w-44"
            />
            <Input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              aria-label={t("auditLogs:filters.fromDate")}
              className="w-full sm:w-40"
            />
            <Input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              aria-label={t("auditLogs:filters.toDate")}
              className="w-full sm:w-40"
            />
          </DataToolbar>

          {auditLogsQuery.isLoading ? <LoadingState /> : null}

          {auditLogsQuery.isError ? (
            <ErrorState
              title={t("auditLogs:errors.loadFailed")}
              description={getApiErrorMessage(
                auditLogsQuery.error,
                t("auditLogs:errors.loadFailed"),
              )}
              retry={
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
              }
            />
          ) : null}

          {auditLogsQuery.isSuccess && !auditLogs.length ? (
            <EmptyState
              title={t("auditLogs:empty.title")}
              description={t("auditLogs:empty.description")}
            />
          ) : null}

          {auditLogs.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.createdAt")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.action")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.entityName")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.entityId")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.description")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.actorUserName")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.ipAddress")}</th>
                  </tr>
                </thead>
                <tbody>
                  {auditLogs.map((logItem) => (
                    <tr key={logItem.id} className="border-t align-top hover:bg-muted/20">
                      <td className="whitespace-nowrap px-3 py-2">
                        <DateTimeText value={logItem.createdAt} />
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant="secondary">{logItem.action}</Badge>
                      </td>
                      <td className="px-3 py-2">
                        <CodeBadge>{logItem.entityName}</CodeBadge>
                      </td>
                      <td className="px-3 py-2">{logItem.entityId || "-"}</td>
                      <td className="max-w-96 px-3 py-2">
                        <span className="line-clamp-2">{logItem.description || "-"}</span>
                      </td>
                      <td className="px-3 py-2">{logItem.actorUserName || "-"}</td>
                      <td className="px-3 py-2">{logItem.ipAddress || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      </SectionCard>
    </section>
  );
}
