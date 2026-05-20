import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { searchOrganizationalUnits } from "@/features/ad-management/api";
import type { AdOrganizationalUnitListItem } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type Props = {
  value: string | null;
  onChange: (distinguishedName: string) => void;
  disabled?: boolean;
  className?: string;
  showFieldLabel?: boolean;
};

export function AdOuSearchCombobox({
  value,
  onChange,
  disabled,
  className,
  showFieldLabel = true,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<AdOrganizationalUnitListItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, 350);

  const ouQuery = useQuery({
    queryKey: ["ad-management", "organizational-units", debouncedSearch],
    queryFn: () =>
      searchOrganizationalUnits({
        search: debouncedSearch.trim() || undefined,
        pageSize: 50,
      }),
    enabled: open && !disabled,
  });

  const items = useMemo(() => ouQuery.data?.items ?? [], [ouQuery.data]);

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
        <Label>{t("adManagement:users.create.fields.ou")} *</Label>
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
                {triggerLabel || t("adManagement:users.create.fields.ouPlaceholder")}
              </span>
              <ChevronDown className="ml-2 size-4 shrink-0 opacity-60" />
            </button>
          </PopoverTrigger>
        </div>
        <PopoverContent className="min-w-[20rem] p-2 sm:min-w-[24rem]" align="start">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("adManagement:users.create.fields.ouSearch")}
            disabled={disabled}
            autoFocus
          />
          <div className="mt-2 max-h-56 overflow-y-auto">
            {ouQuery.isLoading ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:loading")}</p>
            ) : null}
            {ouQuery.isError ? (
              <p className="px-2 py-3 text-sm text-destructive">
                {t("adManagement:users.create.errors.ouLoadFailed")}
              </p>
            ) : null}
            {ouQuery.isSuccess && !items.length ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:users.create.empty.ouNotFound")}
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
