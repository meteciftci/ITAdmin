import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { LoadingState } from "@/components/common/LoadingState";
import { Button } from "@/components/ui/button";
import {
  AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY,
  getAdDeletedObjectRestoreReadiness,
} from "@/features/ad-management/api";
import { AdDeletedObjectRestoreReadinessPanel } from "@/features/ad-management/components/AdDeletedObjectRestoreReadinessPanel";
import type { AdManagementSettings } from "@/features/ad-management/types";

type Props = {
  settings: AdManagementSettings | undefined;
  readOnly: boolean;
};

export function AdDeletedObjectRestoreReadinessCard({ settings, readOnly }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const queryClient = useQueryClient();

  const readinessQuery = useQuery({
    queryKey: AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY,
    queryFn: getAdDeletedObjectRestoreReadiness,
    enabled: Boolean(settings?.isConfigured && settings?.isEnabled),
    refetchOnWindowFocus: false,
  });

  const checkMutation = useMutation({
    mutationFn: getAdDeletedObjectRestoreReadiness,
    onSuccess: (result) => {
      queryClient.setQueryData(AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY, result);
    },
  });

  const activeResult = checkMutation.data ?? readinessQuery.data;

  const isChecking = checkMutation.isPending || readinessQuery.isFetching;

  if (!settings?.isConfigured) {
    return (
      <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
        {t("adManagement:settings.restoreReadiness.settingsIncomplete")}
      </div>
    );
  }

  return (
    <div className="space-y-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <h3 className="text-sm font-semibold">
            {t("adManagement:settings.restoreReadiness.title")}
          </h3>
          <p className="text-xs text-muted-foreground">
            {t("adManagement:settings.restoreReadiness.description")}
          </p>
        </div>
        {!readOnly ? (
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={isChecking || !settings.isEnabled}
            onClick={() => checkMutation.mutate()}
          >
            {t("adManagement:settings.restoreReadiness.check")}
          </Button>
        ) : null}
      </div>

      {!settings.isEnabled ? (
        <p className="text-sm text-muted-foreground">
          {t("adManagement:settings.restoreReadiness.moduleDisabled")}
        </p>
      ) : null}

      {settings.isEnabled && readinessQuery.isLoading && !activeResult ? (
        <LoadingState />
      ) : null}

      {settings.isEnabled && readinessQuery.isError && !activeResult ? (
        <p className="text-sm text-muted-foreground">
          {t("adManagement:deletedObjects.restore.readiness.loadFailed")}
        </p>
      ) : null}

      {settings.isEnabled && activeResult ? (
        <AdDeletedObjectRestoreReadinessPanel
          result={activeResult}
          showRetry={false}
        />
      ) : null}
    </div>
  );
}
