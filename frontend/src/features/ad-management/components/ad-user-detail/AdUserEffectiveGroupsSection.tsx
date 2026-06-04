import { useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Select } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  getAdGroupPathNodeLabel,
  getAdGroupPrimaryLabel,
  getAdGroupSecondaryLabel,
  type AdGroupDisplayFields,
} from "@/features/ad-management/ad-group-display-labels";
import {
  AD_MANAGEMENT_USER_EFFECTIVE_GROUPS_QUERY_KEY,
  getAdUserEffectiveGroups,
} from "@/features/ad-management/api";
import type {
  AdEffectiveGroupNestedItem,
  AdEffectiveGroupSummaryItem,
  AdMembershipPathNode,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

const MAX_DEPTH_OPTIONS = [3, 5, 10] as const;
const DEFAULT_MAX_DEPTH = 5;

type Props = {
  userId: string;
};

function GroupMembershipCard({
  group,
  distinguishedName,
  headerAccessory,
  children,
}: {
  group: AdGroupDisplayFields;
  distinguishedName: string;
  headerAccessory?: ReactNode;
  children?: ReactNode;
}) {
  const primaryLabel = getAdGroupPrimaryLabel(group);
  const secondaryLabel = getAdGroupSecondaryLabel(group, primaryLabel);

  return (
    <li className="space-y-2 rounded-md border bg-card px-3 py-2 text-sm">
      <div className="space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="min-w-0 flex-1 truncate font-medium" title={primaryLabel}>
            {primaryLabel}
          </p>
          {headerAccessory}
        </div>
        {secondaryLabel ? (
          <p className="truncate text-xs text-muted-foreground" title={secondaryLabel}>
            {secondaryLabel}
          </p>
        ) : null}
      </div>
      {children}
      <p
        className="break-all font-mono text-xs text-muted-foreground"
        title={distinguishedName}
      >
        {distinguishedName}
      </p>
    </li>
  );
}

function MembershipPathBreadcrumb({
  path,
  userFallbackLabel,
}: {
  path: AdMembershipPathNode[];
  userFallbackLabel: string;
}) {
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1 text-xs text-muted-foreground">
      {path.map((node, index) => {
        const label = getAdGroupPathNodeLabel(
          {
            displayName: node.displayName,
            name: node.name,
            samAccountName: node.samAccountName,
            distinguishedName: node.distinguishedName,
          },
          node.type === "User" ? userFallbackLabel : undefined,
        );

        return (
          <span
            key={`${node.distinguishedName}-${index}`}
            className="inline-flex min-w-0 items-center gap-1"
          >
            {index > 0 ? <span aria-hidden="true">→</span> : null}
            <span
              className="max-w-[12rem] truncate font-medium text-foreground"
              title={node.distinguishedName}
            >
              {label}
            </span>
          </span>
        );
      })}
    </div>
  );
}

function DirectGroupList({ groups }: { groups: AdEffectiveGroupSummaryItem[] }) {
  return (
    <ul className="max-h-72 space-y-2 overflow-y-auto rounded-md border bg-muted/20 p-2">
      {groups.map((group) => (
        <GroupMembershipCard key={group.distinguishedName} group={group} distinguishedName={group.distinguishedName} />
      ))}
    </ul>
  );
}

function EffectiveGroupList({
  groups,
  t,
  userFallbackLabel,
}: {
  groups: AdEffectiveGroupNestedItem[];
  t: (key: string, options?: Record<string, unknown>) => string;
  userFallbackLabel: string;
}) {
  return (
    <ul className="max-h-96 space-y-3 overflow-y-auto rounded-md border bg-muted/20 p-2">
      {groups.map((group) => (
        <GroupMembershipCard
          key={group.distinguishedName}
          group={group}
          distinguishedName={group.distinguishedName}
          headerAccessory={
            <Badge variant="outline" className="text-xs">
              {t("adManagement:users.detail.effectiveGroups.depth", { depth: group.depth })}
            </Badge>
          }
        >
          <div className="space-y-1">
            <p className="text-xs font-medium text-muted-foreground">
              {t("adManagement:users.detail.effectiveGroups.membershipPath")}
            </p>
            <MembershipPathBreadcrumb path={group.path} userFallbackLabel={userFallbackLabel} />
          </div>
        </GroupMembershipCard>
      ))}
    </ul>
  );
}

export function AdUserEffectiveGroupsSection({ userId }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [maxDepth, setMaxDepth] = useState<number>(DEFAULT_MAX_DEPTH);
  const [activeTab, setActiveTab] = useState<"direct" | "effective">("direct");

  const effectiveGroupsQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USER_EFFECTIVE_GROUPS_QUERY_KEY, userId, maxDepth],
    queryFn: () => getAdUserEffectiveGroups(userId, { maxDepth }),
    staleTime: 0,
  });

  const data = effectiveGroupsQuery.data;
  const directCount = data?.directGroups.length ?? 0;
  const effectiveCount = data?.effectiveGroups.length ?? 0;
  const pathUserFallbackLabel = t("adManagement:users.detail.effectiveGroups.pathUser");

  return (
    <SectionCard
      title={t("adManagement:users.detail.effectiveGroups.title")}
      description={t("adManagement:users.detail.effectiveGroups.description")}
      actions={
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>{t("adManagement:users.detail.effectiveGroups.maxDepth")}</span>
          <Select
            value={String(maxDepth)}
            onChange={(event) => setMaxDepth(Number(event.target.value))}
            className="h-8 w-20"
            aria-label={t("adManagement:users.detail.effectiveGroups.maxDepth")}
          >
            {MAX_DEPTH_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </label>
      }
    >
      {effectiveGroupsQuery.isLoading ? <LoadingState /> : null}

      {effectiveGroupsQuery.isError ? (
        <ErrorState
          title={t("adManagement:users.detail.effectiveGroups.errors.loadFailed")}
          description={getApiErrorMessage(
            effectiveGroupsQuery.error,
            t("adManagement:users.detail.effectiveGroups.errors.loadFailed"),
          )}
        />
      ) : null}

      {effectiveGroupsQuery.isSuccess && data ? (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
            <span>
              {t("adManagement:users.detail.effectiveGroups.directCount", {
                count: directCount,
              })}
            </span>
            <span>
              {t("adManagement:users.detail.effectiveGroups.effectiveCount", {
                count: effectiveCount,
              })}
            </span>
            <span>
              {t("adManagement:users.detail.effectiveGroups.appliedMaxDepth", {
                depth: data.maxDepth,
              })}
            </span>
          </div>

          {data.truncated ? (
            <p className={cn("rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm")}>
              {t("adManagement:users.detail.effectiveGroups.truncated")}
            </p>
          ) : null}

          <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as "direct" | "effective")}>
            <TabsList className="grid w-full max-w-md grid-cols-2">
              <TabsTrigger value="direct">
                {t("adManagement:users.detail.effectiveGroups.tabs.direct")}
              </TabsTrigger>
              <TabsTrigger value="effective">
                {t("adManagement:users.detail.effectiveGroups.tabs.effective")}
              </TabsTrigger>
            </TabsList>

            <TabsContent value="direct" className="mt-3">
              {directCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.directTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.directDescription")}
                />
              ) : (
                <DirectGroupList groups={data.directGroups} />
              )}
            </TabsContent>

            <TabsContent value="effective" className="mt-3">
              {effectiveCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.effectiveTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.effectiveDescription")}
                />
              ) : (
                <EffectiveGroupList
                  groups={data.effectiveGroups}
                  t={t}
                  userFallbackLabel={pathUserFallbackLabel}
                />
              )}
            </TabsContent>
          </Tabs>
        </div>
      ) : null}
    </SectionCard>
  );
}
