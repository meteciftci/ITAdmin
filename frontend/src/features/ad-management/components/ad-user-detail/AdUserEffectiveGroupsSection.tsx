import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, X } from "lucide-react";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  filterEffectiveGroupMemberships,
  normalizeEffectiveGroupSearchText,
} from "@/features/ad-management/ad-effective-group-search";
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
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
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
  const [searchQuery, setSearchQuery] = useState("");

  const effectiveGroupsQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USER_EFFECTIVE_GROUPS_QUERY_KEY, userId, maxDepth],
    queryFn: () => getAdUserEffectiveGroups(userId, { maxDepth }),
    staleTime: 0,
  });

  const data = effectiveGroupsQuery.data;
  const normalizedSearchQuery = useMemo(
    () => normalizeEffectiveGroupSearchText(searchQuery),
    [searchQuery],
  );
  const hasActiveSearch = normalizedSearchQuery.length > 0;

  const filteredMemberships = useMemo(() => {
    if (!data) {
      return { directGroups: [], effectiveGroups: [] };
    }

    return filterEffectiveGroupMemberships(data, searchQuery);
  }, [data, searchQuery]);

  const totalDirectCount = data?.directGroups.length ?? 0;
  const totalEffectiveCount = data?.effectiveGroups.length ?? 0;
  const filteredDirectCount = filteredMemberships.directGroups.length;
  const filteredEffectiveCount = filteredMemberships.effectiveGroups.length;
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
          description={getAdManagementApiErrorMessage(
            effectiveGroupsQuery.error,
            t,
            "adManagement:users.detail.effectiveGroups.errors.loadFailed",
          )}
        />
      ) : null}

      {effectiveGroupsQuery.isSuccess && data ? (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
            <span>
              {hasActiveSearch
                ? t("adManagement:users.detail.effectiveGroups.directCountFiltered", {
                    filtered: filteredDirectCount,
                    total: totalDirectCount,
                  })
                : t("adManagement:users.detail.effectiveGroups.directCount", {
                    count: totalDirectCount,
                  })}
            </span>
            <span>
              {hasActiveSearch
                ? t("adManagement:users.detail.effectiveGroups.effectiveCountFiltered", {
                    filtered: filteredEffectiveCount,
                    total: totalEffectiveCount,
                  })
                : t("adManagement:users.detail.effectiveGroups.effectiveCount", {
                    count: totalEffectiveCount,
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

            <div className="relative mt-3 w-full">
              <Search
                className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden="true"
              />
              <Input
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder={t("adManagement:users.detail.effectiveGroups.searchPlaceholder")}
                className="w-full pr-9 pl-8"
                aria-label={t("adManagement:users.detail.effectiveGroups.searchPlaceholder")}
              />
              {searchQuery ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="absolute top-1/2 right-1 size-7 -translate-y-1/2"
                  onClick={() => setSearchQuery("")}
                  aria-label={t("common:dataTable.clearFilters")}
                >
                  <X className="size-4" />
                </Button>
              ) : null}
            </div>

            <TabsContent value="direct" className="mt-3">
              {totalDirectCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.directTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.directDescription")}
                />
              ) : filteredDirectCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.searchTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.searchDescription")}
                />
              ) : (
                <DirectGroupList groups={filteredMemberships.directGroups} />
              )}
            </TabsContent>

            <TabsContent value="effective" className="mt-3">
              {totalEffectiveCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.effectiveTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.effectiveDescription")}
                />
              ) : filteredEffectiveCount === 0 ? (
                <EmptyState
                  title={t("adManagement:users.detail.effectiveGroups.empty.searchTitle")}
                  description={t("adManagement:users.detail.effectiveGroups.empty.searchDescription")}
                />
              ) : (
                <EffectiveGroupList
                  groups={filteredMemberships.effectiveGroups}
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
