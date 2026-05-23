import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useAuthStore } from "@/features/auth/auth-store";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  addAdUserToGroup,
  AD_MANAGEMENT_USER_GROUPS_QUERY_KEY,
  getAdUserGroups,
  invalidateAdUserGroupsQuery,
  removeAdUserFromGroup,
} from "@/features/ad-management/api";
import { AdGroupSearchCombobox } from "@/features/ad-management/components/AdGroupSearchCombobox";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdGroupSearchItem,
  AdUserGroupMembershipItem,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

export function AdUserGroupsPage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const { id: userId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  const canAddGroup = canAccess(currentUser, "AdManagement.Users.Groups.Add");
  const canRemoveGroup = canAccess(currentUser, "AdManagement.Users.Groups.Remove");

  const [selectedGroup, setSelectedGroup] = useState<AdGroupSearchItem | null>(null);
  const [removeTarget, setRemoveTarget] = useState<AdUserGroupMembershipItem | null>(null);

  const groupsQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USER_GROUPS_QUERY_KEY, userId],
    queryFn: () => getAdUserGroups(userId!),
    enabled: Boolean(userId) && moduleStatus.isOperational,
  });

  const memberGroupDns = useMemo(
    () =>
      new Set(
        (groupsQuery.data?.groups ?? []).map((group) => group.distinguishedName),
      ),
    [groupsQuery.data?.groups],
  );

  const addMutation = useMutation({
    mutationFn: (groupDistinguishedName: string) =>
      addAdUserToGroup(userId!, { groupDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:users.groups.messages.operationFailed"));
        return;
      }

      toast.success(
        response.message || t("adManagement:users.groups.messages.membershipAdded"),
      );
      setSelectedGroup(null);
      await invalidateAdUserGroupsQuery(queryClient, userId!);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.groups.messages.operationFailed")),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (groupDistinguishedName: string) =>
      removeAdUserFromGroup(userId!, { groupDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:users.groups.messages.operationFailed"));
        return;
      }

      toast.success(
        response.message || t("adManagement:users.groups.messages.membershipRemoved"),
      );
      setRemoveTarget(null);
      await invalidateAdUserGroupsQuery(queryClient, userId!);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.groups.messages.operationFailed")),
      );
    },
  });

  if (!userId) {
    return <Navigate to="/ad-management/users" replace />;
  }

  const userSummary = groupsQuery.data;

  return (
    <AdManagementModuleStateGuard>
      <div className="space-y-6">
        <PageHeader
          title={t("adManagement:users.groups.pageTitle")}
          description={t("adManagement:users.groups.pageDescription")}
          actions={
            <Link
              to="/ad-management/users"
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        {groupsQuery.isLoading ? <LoadingState /> : null}

        {groupsQuery.isError ? (
          <ErrorState
            title={t("adManagement:users.groups.errors.loadFailed")}
            description={getApiErrorMessage(
              groupsQuery.error,
              t("adManagement:users.groups.errors.loadFailed"),
            )}
          />
        ) : null}

        {groupsQuery.isSuccess && userSummary ? (
          <>
            <SectionCard
              title={t("adManagement:users.groups.sections.userSummary")}
              description={t("adManagement:users.groups.pageHeading")}
            >
              <div className="grid gap-4 md:grid-cols-2">
                <SummaryField
                  label={t("adManagement:users.detail.displayName")}
                  value={userSummary.displayName}
                />
                <SummaryField
                  label={t("adManagement:users.detail.username")}
                  value={userSummary.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:users.detail.upn")}
                  value={userSummary.userPrincipalName}
                />
                <SummaryField
                  label={t("adManagement:users.groups.table.distinguishedName")}
                  value={userSummary.distinguishedName}
                  mono
                />
              </div>
            </SectionCard>

            {canAddGroup ? (
              <SectionCard
                title={t("adManagement:users.groups.sections.addGroup")}
                description={t("adManagement:users.groups.sections.addGroupDescription")}
              >
                <div className="space-y-4">
                  <AdGroupSearchCombobox
                    value={selectedGroup}
                    onChange={setSelectedGroup}
                    disabledGroupDns={memberGroupDns}
                    disabled={addMutation.isPending}
                  />
                  {selectedGroup ? (
                    <div className="rounded-md border bg-muted/20 p-3 text-sm">
                      <p className="font-medium">{selectedGroup.name}</p>
                      <p className="mt-1 font-mono text-xs text-muted-foreground">
                        {selectedGroup.distinguishedName}
                      </p>
                    </div>
                  ) : null}
                  <Button
                    type="button"
                    disabled={!selectedGroup || addMutation.isPending}
                    onClick={() => {
                      if (!selectedGroup) {
                        return;
                      }

                      addMutation.mutate(selectedGroup.distinguishedName);
                    }}
                  >
                    {t("adManagement:users.groups.actions.addToGroup")}
                  </Button>
                </div>
              </SectionCard>
            ) : null}

            <SectionCard
              title={t("adManagement:users.groups.sections.currentMemberships")}
              description={t("adManagement:users.groups.sections.currentMembershipsDescription")}
            >
              {userSummary.groups.length === 0 ? (
                <EmptyState
                  title={t("adManagement:users.groups.empty.noMembershipsTitle")}
                  description={t("adManagement:users.groups.empty.noMembershipsDescription")}
                />
              ) : (
                <div className="overflow-x-auto rounded-md border">
                  <table className="w-full min-w-[48rem] text-sm">
                    <thead className="bg-muted/40 text-left">
                      <tr>
                        <th className="px-3 py-2 font-medium">
                          {t("adManagement:users.groups.table.groupName")}
                        </th>
                        <th className="px-3 py-2 font-medium">
                          {t("adManagement:users.groups.table.samAccountName")}
                        </th>
                        <th className="px-3 py-2 font-medium">
                          {t("adManagement:users.groups.table.description")}
                        </th>
                        <th className="px-3 py-2 font-medium">
                          {t("adManagement:users.groups.table.distinguishedName")}
                        </th>
                        {canRemoveGroup ? (
                          <th className="px-3 py-2 font-medium">
                            {t("adManagement:users.table.actions")}
                          </th>
                        ) : null}
                      </tr>
                    </thead>
                    <tbody>
                      {userSummary.groups.map((group) => (
                        <tr key={group.distinguishedName} className="border-t align-top">
                          <td className="px-3 py-2">{group.name}</td>
                          <td className="px-3 py-2">{group.samAccountName || "-"}</td>
                          <td className="max-w-xs px-3 py-2">{group.description || "-"}</td>
                          <td className="max-w-md px-3 py-2">
                            <span className="font-mono text-xs text-muted-foreground">
                              {group.distinguishedName}
                            </span>
                          </td>
                          {canRemoveGroup ? (
                            <td className="px-3 py-2">
                              <Button
                                type="button"
                                variant="destructive"
                                size="sm"
                                disabled={removeMutation.isPending}
                                onClick={() => setRemoveTarget(group)}
                              >
                                {t("adManagement:users.groups.actions.removeFromGroup")}
                              </Button>
                            </td>
                          ) : null}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </SectionCard>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={removeTarget !== null}
        onOpenChange={(open) => {
          if (!open) {
            setRemoveTarget(null);
          }
        }}
        title={t("adManagement:users.groups.confirm.removeTitle")}
        description={t("adManagement:users.groups.confirm.removeDescription")}
        confirmText={t("adManagement:users.groups.confirm.removeConfirm")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={removeMutation.isPending}
        onConfirm={() => {
          if (!removeTarget) {
            return;
          }

          removeMutation.mutate(removeTarget.distinguishedName);
        }}
      />
    </AdManagementModuleStateGuard>
  );
}

function SummaryField({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string | null | undefined;
  mono?: boolean;
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={cn(mono && "font-mono text-xs break-all")}>{value || "-"}</p>
    </div>
  );
}
