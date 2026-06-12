import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  formatAdGroupSelectionPrimaryLabel,
  formatAdGroupSelectionSecondaryLabel,
} from "@/features/ad-management/ad-group-display";
import { searchAdComputerGroupCandidates } from "@/features/ad-management/api";
import type { AdComputerGroupCandidateItem } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

const MIN_SEARCH_LENGTH = 2;

type Props = {
  computerId: string;
  value: AdComputerGroupCandidateItem | null;
  onChange: (group: AdComputerGroupCandidateItem | null) => void;
  disabledGroupDns: ReadonlySet<string>;
  disabled?: boolean;
};

export function AdComputerGroupSearchCombobox({
  computerId,
  value,
  onChange,
  disabledGroupDns,
  disabled,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const groupsQuery = useQuery({
    queryKey: ["ad-management", "computers", computerId, "group-candidates", normalizedSearch],
    queryFn: () => searchAdComputerGroupCandidates(computerId, normalizedSearch),
    enabled: open && canSearch && !disabled,
  });

  const items = useMemo(() => groupsQuery.data?.items ?? [], [groupsQuery.data]);

  const triggerLabel = useMemo(() => {
    if (!value) {
      return "";
    }

    return formatAdGroupSelectionPrimaryLabel(value);
  }, [value]);

  function handleSelect(item: AdComputerGroupCandidateItem) {
    if (disabledGroupDns.has(item.distinguishedName)) {
      return;
    }

    onChange(item);
    setOpen(false);
    setSearch("");
  }

  return (
    <div className="space-y-1.5">
      <Label>{t("adManagement:computers.groups.fields.searchGroup")}</Label>
      <Popover open={open} onOpenChange={setOpen}>
        <div className="w-full [&>span]:flex [&>span]:w-full">
          <PopoverTrigger asChild>
            <button
              type="button"
              disabled={disabled}
              className={cn(
                "flex h-10 w-full min-w-0 items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-left text-sm shadow-xs",
                "hover:bg-muted/30 disabled:cursor-not-allowed disabled:opacity-50",
              )}
            >
              <span
                className={cn(
                  "truncate",
                  !triggerLabel && "text-muted-foreground",
                )}
              >
                {triggerLabel || t("adManagement:computers.groups.fields.selectGroup")}
              </span>
              <ChevronDown className="ml-2 size-4 shrink-0 opacity-50" />
            </button>
          </PopoverTrigger>
        </div>
        <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-2" align="start">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("adManagement:computers.groups.fields.searchGroupPlaceholder")}
            disabled={disabled}
          />
          <div className="mt-2 max-h-60 overflow-y-auto">
            {!canSearch ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:computers.empty.searchRequired")}
              </p>
            ) : null}
            {canSearch && groupsQuery.isLoading ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("common:loading.default")}
              </p>
            ) : null}
            {canSearch && groupsQuery.isError ? (
              <p className="px-2 py-3 text-sm text-destructive">
                {t("adManagement:computers.groups.errors.candidateSearchFailed")}
              </p>
            ) : null}
            {canSearch && groupsQuery.isSuccess && items.length === 0 ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:computers.groups.empty.noCandidates")}
              </p>
            ) : null}
            {canSearch && groupsQuery.isSuccess
              ? items.map((item) => {
                  const isDisabled = disabledGroupDns.has(item.distinguishedName);
                  const primaryLabel = formatAdGroupSelectionPrimaryLabel(item);
                  const secondaryLabel = formatAdGroupSelectionSecondaryLabel(item);

                  return (
                    <button
                      key={item.distinguishedName}
                      type="button"
                      disabled={isDisabled || disabled}
                      onClick={() => handleSelect(item)}
                      className={cn(
                        "flex w-full flex-col rounded-md px-2 py-2 text-left text-sm hover:bg-muted/50",
                        isDisabled && "cursor-not-allowed opacity-50",
                      )}
                    >
                      <span className="font-medium">{primaryLabel}</span>
                      {secondaryLabel ? (
                        <span className="text-xs text-muted-foreground">{secondaryLabel}</span>
                      ) : null}
                    </button>
                  );
                })
              : null}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
