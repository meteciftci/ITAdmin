import { useMemo } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  getAdOperationStatusBadgeVariant,
} from "@/features/ad-management/ad-operation-log-columns";
import {
  getAdOperationErrorSummary,
  parseAdOperationErrorMessage,
} from "@/features/ad-management/parse-ad-operation-error-message";
import {
  AD_OPERATION_LOGS_QUERY_KEY,
  getAdOperationLogs,
} from "@/features/ad-management/operation-logs-api";
import { cn } from "@/lib/utils";

const RECENT_OPERATIONS_PAGE_SIZE = 5;
const AD_OPERATION_LOGS_PATH = "/monitoring/module-logs/ad-operation-logs";

type Props = {
  userId: string;
  enabled: boolean;
};

export function AdUserRecentOperationsSection({ userId, enabled }: Props) {
  const { t } = useTranslation(["adManagement", "adOperationLogs", "common"]);

  const logsQuery = useQuery({
    queryKey: [
      ...AD_OPERATION_LOGS_QUERY_KEY,
      "recent",
      userId,
      RECENT_OPERATIONS_PAGE_SIZE,
    ],
    queryFn: () =>
      getAdOperationLogs({
        targetObjectGuid: userId,
        pageNumber: 1,
        pageSize: RECENT_OPERATIONS_PAGE_SIZE,
      }),
    enabled,
  });

  const getOperationLabel = (value: string) => {
    const key = `operations.${value}` as const;
    const translated = t(`adOperationLogs:${key}`, { defaultValue: "" });
    return translated || value;
  };

  const getStatusLabel = (status: string) => {
    const key = `statuses.${status}` as const;
    const translated = t(`adOperationLogs:${key}`, { defaultValue: "" });
    return translated || status;
  };

  const logs = useMemo(() => logsQuery.data?.items ?? [], [logsQuery.data?.items]);

  return (
    <SectionCard
      title={t("adManagement:users.detail.page.recentOperations")}
      actions={
        <Link
          to={AD_OPERATION_LOGS_PATH}
          className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
        >
          {t("adManagement:users.detail.page.viewAllLogs")}
        </Link>
      }
    >
      {logsQuery.isLoading ? <LoadingState /> : null}

      {logsQuery.isSuccess && logs.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          {t("adManagement:users.detail.page.noRecentOperations")}
        </p>
      ) : null}

      {logsQuery.isSuccess && logs.length > 0 ? (
        <div className="overflow-x-auto rounded-md border">
          <table className="min-w-full text-sm">
            <thead className="bg-muted/50 text-left">
              <tr>
                <th className="px-3 py-2 font-medium whitespace-nowrap">
                  {t("adOperationLogs:table.createdAt")}
                </th>
                <th className="px-3 py-2 font-medium whitespace-nowrap">
                  {t("adOperationLogs:table.operation")}
                </th>
                <th className="px-3 py-2 font-medium whitespace-nowrap">
                  {t("adOperationLogs:table.status")}
                </th>
                <th className="px-3 py-2 font-medium whitespace-nowrap">
                  {t("adOperationLogs:table.actor")}
                </th>
                <th className="px-3 py-2 font-medium whitespace-nowrap">
                  {t("adOperationLogs:table.errorSummary")}
                </th>
              </tr>
            </thead>
            <tbody>
              {logs.map((item) => {
                const statusLabel = getStatusLabel(item.status);
                const summary =
                  !item.hasError && !item.errorMessage
                    ? null
                    : getAdOperationErrorSummary(
                        parseAdOperationErrorMessage(item.errorMessage),
                      );

                return (
                  <tr key={item.id} className="border-t align-top hover:bg-muted/20">
                    <td className="px-3 py-2 whitespace-nowrap">
                      <DateTimeText value={item.createdAt} />
                    </td>
                    <td className="px-3 py-2">
                      <Badge variant="outline">
                        {getOperationLabel(item.operationType)}
                      </Badge>
                    </td>
                    <td className="px-3 py-2">
                      <Badge
                        variant={getAdOperationStatusBadgeVariant(item.status, null)}
                      >
                        {statusLabel}
                      </Badge>
                    </td>
                    <td className="px-3 py-2">
                      {item.actorUserName || "-"}
                    </td>
                    <td className="px-3 py-2">
                      {summary ? (
                        <span
                          className="line-clamp-2 text-xs text-muted-foreground"
                          title={summary}
                        >
                          {summary}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </SectionCard>
  );
}
