import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { LoadingState } from "@/components/common/LoadingState";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  getAdGroupScopeLabel,
  getAdGroupTypeLabel,
} from "@/features/ad-management/ad-group-labels";
import {
  AD_MANAGEMENT_GROUPS_QUERY_KEY,
  deleteAdGroup,
  getAdGroupById,
  invalidateAdManagementGroupQueries,
} from "@/features/ad-management/api";
import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import type { AdGroupDetail } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  open: boolean;
  groupId: string | null;
  onOpenChange: (open: boolean) => void;
  onDeleted?: () => void;
};

function resolveConfirmationValue(group: AdGroupDetail): string {
  return group.samAccountName?.trim() || group.name.trim();
}

export function AdDeleteGroupConfirmDialog({
  open,
  groupId,
  onOpenChange,
  onDeleted,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [confirmValue, setConfirmValue] = useState("");

  const groupQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_GROUPS_QUERY_KEY, "delete-confirm", groupId],
    queryFn: () => getAdGroupById(groupId!),
    enabled: open && Boolean(groupId?.trim()),
    staleTime: 0,
  });

  const group = groupQuery.data;
  const expectedConfirmValue = group ? resolveConfirmationValue(group) : "";
  const isConfirmMatch =
    expectedConfirmValue.length > 0
    && confirmValue.trim().toLowerCase() === expectedConfirmValue.toLowerCase();

  const deleteMutation = useMutation({
    mutationFn: async () => {
      if (!groupId) {
        throw new Error("Missing group id");
      }
      return deleteAdGroup(groupId);
    },
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:groups.delete.error"));
        return;
      }

      await invalidateAdManagementGroupQueries(queryClient);
      await queryClient.invalidateQueries({ queryKey: AD_OPERATION_LOGS_QUERY_KEY });
      toast.success(response.message || t("adManagement:groups.delete.success"));
      setConfirmValue("");
      onOpenChange(false);
      onDeleted?.();
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("adManagement:groups.delete.error")));
    },
  });

  const summaryEntries = useMemo(() => {
    if (!group) {
      return [];
    }

    return [
      {
        label: t("adManagement:groups.table.displayName"),
        value: group.displayName,
      },
      {
        label: t("adManagement:groups.table.name"),
        value: group.name,
      },
      {
        label: t("adManagement:groups.table.samAccountName"),
        value: group.samAccountName,
      },
      {
        label: t("adManagement:groups.table.distinguishedName"),
        value: group.distinguishedName,
        mono: true,
      },
      {
        label: t("adManagement:groups.delete.memberCount"),
        value: String(group.memberCount),
      },
      {
        label: t("adManagement:groups.delete.memberOfCount"),
        value: String(group.memberOfCount),
      },
      {
        label: t("adManagement:groups.table.scope"),
        value: getAdGroupScopeLabel(t, group.groupScope),
      },
      {
        label: t("adManagement:groups.detail.securityEnabled"),
        value: getAdGroupTypeLabel(t, group.securityEnabled),
      },
    ];
  }, [group, t]);

  return (
    <Dialog open={open}>
      <DialogContent
        onOpenChange={(nextOpen) => {
          if (!deleteMutation.isPending) {
            if (!nextOpen) {
              setConfirmValue("");
            }
            onOpenChange(nextOpen);
          }
        }}
      >
        <DialogHeader>
          <DialogTitle>{t("adManagement:groups.delete.title")}</DialogTitle>
          <DialogDescription>{t("adManagement:groups.delete.description")}</DialogDescription>
        </DialogHeader>

        <DialogBody>
        {groupQuery.isLoading ? <LoadingState /> : null}

        {groupQuery.isError ? (
          <p className="text-sm text-destructive">
            {getApiErrorMessage(groupQuery.error, t("adManagement:groups.delete.error"))}
          </p>
        ) : null}

        {group ? (
          <div className="space-y-4">
            <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-2">
              {summaryEntries.map((entry) => (
                <div key={entry.label} className="space-y-1">
                  <p className="text-xs text-muted-foreground">{entry.label}</p>
                  <p
                    className={
                      entry.mono
                        ? "break-all font-mono text-xs text-muted-foreground"
                        : "break-all text-sm"
                    }
                  >
                    {entry.value || "-"}
                  </p>
                </div>
              ))}
            </div>

            <div className="space-y-2">
              <Label htmlFor="delete-group-confirm">
                {t("adManagement:groups.delete.confirmLabel")}
              </Label>
              <Input
                id="delete-group-confirm"
                value={confirmValue}
                onChange={(event) => setConfirmValue(event.target.value)}
                placeholder={t("adManagement:groups.delete.confirmPlaceholder")}
                autoComplete="off"
                disabled={deleteMutation.isPending}
              />
            </div>
          </div>
        ) : null}
        </DialogBody>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => {
              setConfirmValue("");
              onOpenChange(false);
            }}
            disabled={deleteMutation.isPending}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={() => deleteMutation.mutate()}
            disabled={!group || !isConfirmMatch || deleteMutation.isPending}
          >
            {t("adManagement:groups.delete.confirmButton")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
