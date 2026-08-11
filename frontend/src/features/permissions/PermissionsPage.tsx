import { useMemo, useState } from "react";

import { useQuery } from "@tanstack/react-query";
import { ChevronDown, KeyRound, Layers3 } from "lucide-react";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DataTableToolbar } from "@/components/common/data-table";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { getPermissionCatalog } from "@/features/permissions/api";
import {
  groupPermissionsByModule,
  type PermissionModuleGroup,
} from "@/features/permissions/permission-catalog";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

type StatusFilter = "active" | "passive" | "all";

type PermissionModuleSectionProps = {
  group: PermissionModuleGroup;
  index: number;
  label: string;
  countLabel: string;
  noDescriptionLabel: string;
};

function PermissionModuleSection({
  group,
  index,
  label,
  countLabel,
  noDescriptionLabel,
}: PermissionModuleSectionProps) {
  const [isOpen, setIsOpen] = useState(true);
  const contentId = `permission-module-content-${index}`;

  return (
    <section
      id={`permission-module-${index}`}
      className="scroll-mt-24 overflow-hidden rounded-xl border bg-card shadow-sm"
    >
      <button
        type="button"
        className="flex min-h-14 w-full items-center justify-between gap-4 px-4 py-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring sm:px-5"
        aria-expanded={isOpen}
        aria-controls={contentId}
        onClick={() => setIsOpen((current) => !current)}
      >
        <span className="flex min-w-0 items-center gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
            <Layers3 className="size-4" aria-hidden />
          </span>
          <span className="min-w-0">
            <span className="block truncate font-heading text-base font-semibold">
              {label}
            </span>
            <span className="block text-xs text-muted-foreground">{countLabel}</span>
          </span>
        </span>
        <ChevronDown
          className={`size-4 shrink-0 text-muted-foreground transition-transform ${
            isOpen ? "rotate-180" : ""
          }`}
          aria-hidden
        />
      </button>

      {isOpen ? (
        <ul id={contentId} className="divide-y border-t">
          {group.items.map((permission) => (
            <li
              key={permission.id}
              className="grid gap-3 px-4 py-4 transition-colors hover:bg-muted/30 sm:grid-cols-[minmax(13rem,0.8fr)_minmax(16rem,1.2fr)_auto] sm:items-start sm:px-5"
            >
              <div className="min-w-0 space-y-1.5">
                <p className="font-medium leading-5">{permission.name}</p>
                <CodeBadge className="max-w-full break-all whitespace-normal">
                  {permission.code}
                </CodeBadge>
              </div>
              <p className="text-sm leading-6 text-muted-foreground">
                {permission.description || noDescriptionLabel}
              </p>
              <StatusBadge isActive={permission.isActive} />
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

export function PermissionsPage() {
  const { t } = useTranslation(["permissions", "common"]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");

  const debouncedSearch = useDebouncedValue(search, 300).trim();
  const effectiveSearch = debouncedSearch || undefined;

  const permissionsQuery = useQuery({
    queryKey: ["permissions", "catalog", effectiveSearch, statusFilter],
    queryFn: () =>
      getPermissionCatalog({
        search: effectiveSearch,
        isActive:
          statusFilter === "all"
            ? undefined
            : statusFilter === "active",
      }),
  });

  const groups = useMemo(
    () => groupPermissionsByModule(permissionsQuery.data?.items ?? []),
    [permissionsQuery.data],
  );

  if (permissionsQuery.isError) {
    const routeState = createApiErrorRouteState(permissionsQuery.error, {
      fromPath: "/permissions",
      retryPath: "/permissions",
      sourceLabel: t("permissions:sections.catalogTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <PageContainer variant="wide">
      <PageHeader
        title={t("permissions:title")}
        description={t("permissions:description")}
        actions={
          <Button variant="outline" onClick={() => permissionsQuery.refetch()}>
            {t("common:actions.refresh")}
          </Button>
        }
      />

      <DataTableToolbar
        searchValue={search}
        onSearchChange={setSearch}
        searchPlaceholder={t("permissions:search.placeholder")}
        activeFilterCount={statusFilter === "active" ? 0 : 1}
        onClearFilters={() => setStatusFilter("active")}
        activeFilters={
          statusFilter === "active"
            ? []
            : [
                {
                  id: "status",
                  label: t("common:fields.status"),
                  value: t(`common:status.${statusFilter}`),
                  onRemove: () => setStatusFilter("active"),
                },
              ]
        }
        filterContent={
          <Select
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
            aria-label={t("common:fields.status")}
          >
            <option value="active">{t("common:status.active")}</option>
            <option value="passive">{t("common:status.passive")}</option>
            <option value="all">{t("common:status.all")}</option>
          </Select>
        }
      />

      {permissionsQuery.isLoading ? <LoadingState /> : null}

      {permissionsQuery.isSuccess && !groups.length ? (
        <EmptyState
          title={t("permissions:empty.title")}
          description={t("permissions:empty.description")}
        />
      ) : null}

      {groups.length ? (
        <div className="flex min-w-0 flex-col gap-5">
          <div className="grid gap-3 sm:grid-cols-2">
            <Card size="sm">
              <CardContent className="flex items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <KeyRound className="size-5" aria-hidden />
                </span>
                <div>
                  <p className="text-2xl font-semibold tracking-tight">
                    {permissionsQuery.data?.totalCount ?? 0}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {t("permissions:summary.permissions")}
                  </p>
                </div>
              </CardContent>
            </Card>
            <Card size="sm">
              <CardContent className="flex items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Layers3 className="size-5" aria-hidden />
                </span>
                <div>
                  <p className="text-2xl font-semibold tracking-tight">{groups.length}</p>
                  <p className="text-sm text-muted-foreground">
                    {t("permissions:summary.modules")}
                  </p>
                </div>
              </CardContent>
            </Card>
          </div>

          <nav
            aria-label={t("permissions:moduleNavigation.label")}
            className="flex gap-2 overflow-x-auto pb-1"
          >
            {groups.map((group, index) => (
              <a
                key={group.module}
                href={`#permission-module-${index}`}
                className="inline-flex h-9 shrink-0 items-center gap-2 rounded-lg border bg-card px-3 text-sm font-medium shadow-sm transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {t(`permissions:modules.${group.module}`, {
                  defaultValue: group.module,
                })}
                <Badge variant="outline">{group.items.length}</Badge>
              </a>
            ))}
          </nav>

          <div className="space-y-4">
            {groups.map((group, index) => (
              <PermissionModuleSection
                key={`${group.module}-${effectiveSearch ?? "all"}-${statusFilter}`}
                group={group}
                index={index}
                label={t(`permissions:modules.${group.module}`, {
                  defaultValue: group.module,
                })}
                countLabel={t("permissions:module.permissionCount", {
                  count: group.items.length,
                })}
                noDescriptionLabel={t("permissions:item.noDescription")}
              />
            ))}
          </div>
        </div>
      ) : null}
    </PageContainer>
  );
}
