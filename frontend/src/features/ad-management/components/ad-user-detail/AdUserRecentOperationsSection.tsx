import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  getAdOperationStatusBadgeVariant,
} from "@/features/ad-management/ad-operation-log-columns";
import { AdOperationLogDetailDialog } from "@/features/ad-management/components/AdOperationLogDetailDialog";
import { useAdOperationLogLabels } from "@/features/ad-management/hooks/useAdOperationLogLabels";
import { buildAdOperationLogsPath } from "@/features/ad-management/operation-logs-path";
import {
  getAdOperationErrorSummary,
  parseAdOperationErrorMessage,
} from "@/features/ad-management/parse-ad-operation-error-message";
import {
  AD_OPERATION_LOGS_QUERY_KEY,
  getAdOperationLogById,
  getAdOperationLogs,
} from "@/features/ad-management/operation-logs-api";
import { cn } from "@/lib/utils";

const RECENT_OPERATIONS_PAGE_SIZE = 5;

type Props = {
  userId: string;
  enabled: boolean;
};

export function AdUserRecentOperationsSection({ userId, enabled }: Props) {
  const { t } = useTranslation(["adManagement", "adOperationLogs", "common"]);
  const { getOperationLabel, getStatusLabel } = useAdOperationLogLabels();
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

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

  const detailQuery = useQuery({
    queryKey: [...AD_OPERATION_LOGS_QUERY_KEY, "detail", selectedLogId],
    queryFn: () => getAdOperationLogById(selectedLogId!),
    enabled: detailOpen && Boolean(selectedLogId),
  });

  const logs = useMemo(() => logsQuery.data?.items ?? [], [logsQuery.data?.items]);

  return (
    <>
      <SectionCard
        title={t("adManagement:users.detail.page.recentOperations")}
        actions={
          <Link
            to={buildAdOperationLogsPath(userId)}
            className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
          >
            {t("adManagement:users.detail.page.viewAllLogs")}
          </Link>
        }
      >
        {logsQuery.isLoading ? <LoadingState /> : null}

        {logsQuery.isError ? (
          <p className="text-sm text-muted-foreground">
            {t("adManagement:users.messages.operationFailed")}
          </p>
        ) : null}

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
                  <th className="px-3 py-2 font-medium whitespace-nowrap text-right">
                    {t("adManagement:users.detail.page.operationDetail")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {logs.map((item) => {
                  const statusLabel = getStatusLabel(item.status, null);
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
                      <td className="px-3 py-2">{item.actorUserName || "-"}</td>
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
                      <td className="px-3 py-2 text-right">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setSelectedLogId(item.id);
                            setDetailOpen(true);
                          }}
                        >
                          {t("common:actions.detail")}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : null}
      </SectionCard>

      <AdOperationLogDetailDialog
        open={detailOpen}
        onOpenChange={(open) => {
          setDetailOpen(open);
          if (!open) {
            setSelectedLogId(null);
          }
        }}
        detail={detailQuery.data}
        isLoading={detailQuery.isLoading}
        getOperationLabel={getOperationLabel}
        getStatusLabel={getStatusLabel}
      />
    </>
  );
}
