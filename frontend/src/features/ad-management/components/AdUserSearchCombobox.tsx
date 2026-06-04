import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { getAdUsers } from "@/features/ad-management/api";
import type { AdUserListItem } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

const MIN_SEARCH_LENGTH = 2;

type Props = {
  value: AdUserListItem | null;
  onChange: (user: AdUserListItem | null) => void;
  excludeUserId?: string;
  disabled?: boolean;
  label?: string;
  placeholder?: string;
};

function formatUserPrimaryLabel(user: AdUserListItem): string {
  return user.displayName || user.samAccountName || user.userPrincipalName || user.id;
}

function formatUserSecondaryLabel(user: AdUserListItem): string | null {
  if (user.samAccountName && user.userPrincipalName) {
    return `${user.samAccountName} · ${user.userPrincipalName}`;
  }

  return user.samAccountName || user.userPrincipalName || null;
}

export function AdUserSearchCombobox({
  value,
  onChange,
  excludeUserId,
  disabled,
  label,
  placeholder,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const usersQuery = useQuery({
    queryKey: ["ad-management", "users", "search", normalizedSearch],
    queryFn: () =>
      getAdUsers({
        search: normalizedSearch,
        status: "all",
        pageNumber: 1,
        pageSize: 20,
      }),
    enabled: open && canSearch && !disabled,
  });

  const items = useMemo(() => {
    const rawItems = usersQuery.data?.items ?? [];
    if (!excludeUserId) {
      return rawItems;
    }

    return rawItems.filter((item) => item.id !== excludeUserId);
  }, [excludeUserId, usersQuery.data?.items]);

  const triggerLabel = useMemo(() => {
    if (!value) {
      return "";
    }

    return formatUserPrimaryLabel(value);
  }, [value]);

  function handleSelect(item: AdUserListItem) {
    onChange(item);
    setOpen(false);
    setSearch("");
  }

  return (
    <div className="space-y-1.5">
      <Label>{label ?? t("adManagement:users.detail.manager.selectManager")}</Label>
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
                  "min-w-0 flex-1 truncate",
                  !triggerLabel && "text-muted-foreground",
                )}
              >
                {triggerLabel
                  || placeholder
                  || t("adManagement:users.detail.manager.selectManager")}
              </span>
              <ChevronDown className="ml-2 size-4 shrink-0 opacity-60" />
            </button>
          </PopoverTrigger>
        </div>
        <PopoverContent matchTriggerWidth className="p-2" align="start">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("adManagement:users.detail.manager.searchPlaceholder")}
            disabled={disabled}
            autoFocus
          />
          <div className="mt-2 max-h-56 overflow-y-auto">
            {!canSearch ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:users.groups.empty.searchMinLength")}
              </p>
            ) : null}
            {canSearch && usersQuery.isLoading ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:loading")}</p>
            ) : null}
            {canSearch && !usersQuery.isLoading && items.length === 0 ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:users.empty.title")}
              </p>
            ) : null}
            {canSearch && !usersQuery.isLoading
              ? items.map((item) => {
                  const primaryLabel = formatUserPrimaryLabel(item);
                  const secondaryLabel = formatUserSecondaryLabel(item);

                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => handleSelect(item)}
                      className="flex w-full min-w-0 flex-col gap-0.5 rounded-md px-2 py-2 text-left text-sm hover:bg-muted/60"
                    >
                      <span className="truncate font-medium">{primaryLabel}</span>
                      {secondaryLabel ? (
                        <span className="truncate text-xs text-muted-foreground">
                          {secondaryLabel}
                        </span>
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
