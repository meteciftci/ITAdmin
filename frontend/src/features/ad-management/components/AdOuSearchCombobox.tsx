import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  searchComputerOrganizationalUnits,
  searchGroupOrganizationalUnits,
  getAdOrganizationalUnits,
  searchOrganizationalUnits,
} from "@/features/ad-management/api";
import type { AdOrganizationalUnitListItem } from "@/features/ad-management/types";
import { isInvalidOrganizationalUnitMoveTarget } from "@/features/ad-management/ad-ldap-dn";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type OuSearchContext = "users" | "groups" | "computers" | "manage";

type Props = {
  value: string | null;
  onChange: (distinguishedName: string) => void;
  disabled?: boolean;
  className?: string;
  showFieldLabel?: boolean;
  searchContext?: OuSearchContext;
  fieldLabelKey?: string;
  placeholderKey?: string;
  searchKey?: string;
  emptyKey?: string;
  errorKey?: string;
  excludeDistinguishedName?: string | null;
};

export function AdOuSearchCombobox({
  value,
  onChange,
  disabled,
  className,
  showFieldLabel = true,
  searchContext = "users",
  fieldLabelKey = "adManagement:users.create.fields.ou",
  placeholderKey = "adManagement:users.create.fields.ouPlaceholder",
  searchKey = "adManagement:users.create.fields.ouSearch",
  emptyKey = "adManagement:users.create.empty.ouNotFound",
  errorKey = "adManagement:users.create.errors.ouLoadFailed",
  excludeDistinguishedName = null,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<AdOrganizationalUnitListItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, 350);

  const ouQuery = useQuery({
    queryKey: ["ad-management", "organizational-units", searchContext, debouncedSearch],
    queryFn: async () => {
      const params = {
        search: debouncedSearch.trim() || undefined,
        pageSize: 50,
      };

      if (searchContext === "groups") {
        return searchGroupOrganizationalUnits(params);
      }

      if (searchContext === "computers") {
        return searchComputerOrganizationalUnits(params.search, params.pageSize);
      }

      if (searchContext === "manage") {
        const response = await getAdOrganizationalUnits({
          search: params.search,
          pageNumber: 1,
          pageSize: params.pageSize,
        });
        return {
          items: response.items.map(
            (item): AdOrganizationalUnitListItem => ({
              distinguishedName: item.distinguishedName,
              name: item.name,
              displayName: item.name,
              ou: item.ou,
              label: item.name?.trim() || item.ou?.trim() || item.canonicalName,
            }),
          ),
          hasMore: response.hasNextPage,
        };
      }

      return searchOrganizationalUnits(params);
    },
    enabled: open && !disabled,
  });

  const items = useMemo(() => {
    const rawItems = ouQuery.data?.items ?? [];
    if (!excludeDistinguishedName?.trim()) {
      return rawItems;
    }

    return rawItems.filter(
      (item) => !isInvalidOrganizationalUnitMoveTarget(excludeDistinguishedName, item.distinguishedName),
    );
  }, [excludeDistinguishedName, ouQuery.data]);

  const triggerLabel = useMemo(() => {
    if (!value) {
      return "";
    }

    if (selectedItem?.distinguishedName === value) {
      return selectedItem.label;
    }

    const match = items.find((item) => item.distinguishedName === value);
    return match?.label ?? value;
  }, [items, selectedItem, value]);

  function handleSelect(item: AdOrganizationalUnitListItem) {
    setSelectedItem(item);
    onChange(item.distinguishedName);
    setOpen(false);
    setSearch("");
  }

  return (
    <div className={cn("w-full min-w-0 space-y-1.5", className)}>
      {showFieldLabel ? (
        <Label>{t(fieldLabelKey)} *</Label>
      ) : null}
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
              <span className={cn("min-w-0 flex-1 truncate", !triggerLabel && "text-muted-foreground")}>
                {triggerLabel || t(placeholderKey)}
              </span>
              <ChevronDown className="ml-2 size-4 shrink-0 opacity-60" />
            </button>
          </PopoverTrigger>
        </div>
        <PopoverContent className="min-w-[20rem] p-2 sm:min-w-[24rem]" align="start">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t(searchKey)}
            disabled={disabled}
            autoFocus
          />
          <div className="mt-2 max-h-56 overflow-y-auto">
            {ouQuery.isLoading ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:loading")}</p>
            ) : null}
            {ouQuery.isError ? (
              <p className="px-2 py-3 text-sm text-destructive">
                {t(errorKey)}
              </p>
            ) : null}
            {ouQuery.isSuccess && !items.length ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t(emptyKey)}
              </p>
            ) : null}
            {items.length > 0 ? (
              <ul className="space-y-1">
                {items.map((item) => (
                  <li key={item.distinguishedName}>
                    <button
                      type="button"
                      className={cn(
                        "w-full rounded-md px-2 py-2 text-left text-sm hover:bg-muted",
                        value === item.distinguishedName && "bg-muted",
                      )}
                      onClick={() => handleSelect(item)}
                    >
                      <OuListItem item={item} />
                    </button>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}

function OuListItem({ item }: { item: AdOrganizationalUnitListItem }) {
  return (
    <div className="space-y-0.5">
      <div className="font-medium">{item.label}</div>
      <div className="truncate text-xs text-muted-foreground" title={item.distinguishedName}>
        {item.distinguishedName}
      </div>
    </div>
  );
}
