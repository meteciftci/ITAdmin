import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

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
import {
  AD_MANAGEMENT_SETTINGS_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdManagementSettingsOrganizationalUnits,
} from "@/features/ad-management/api";
import type { AdOrganizationalUnitListItem } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

const MIN_SEARCH_LENGTH = 2;
const PAGE_SIZE = 50;

type Props = {
  value: string | null;
  onChange: (distinguishedName: string | null) => void;
  disabled?: boolean;
  allowClear?: boolean;
  labelKey: string;
  descriptionKey: string;
  placeholderKey: string;
  className?: string;
};

function mapSettingsOuItems(
  items: Awaited<ReturnType<typeof getAdManagementSettingsOrganizationalUnits>>["items"],
): AdOrganizationalUnitListItem[] {
  return items.map((item) => ({
    distinguishedName: item.distinguishedName,
    name: item.name,
    displayName: item.name,
    ou: item.ou,
    label: item.name?.trim() || item.ou?.trim() || item.canonicalName,
  }));
}

export function AdSettingsOuPickerField({
  value,
  onChange,
  disabled,
  allowClear = true,
  labelKey,
  descriptionKey,
  placeholderKey,
  className,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<AdOrganizationalUnitListItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const ouQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_SETTINGS_ORGANIZATIONAL_UNITS_QUERY_KEY,
      normalizedSearch,
    ],
    queryFn: async () => {
      const response = await getAdManagementSettingsOrganizationalUnits({
        search: normalizedSearch,
        pageNumber: 1,
        pageSize: PAGE_SIZE,
      });
      return {
        items: mapSettingsOuItems(response.items),
        hasMore: response.hasNextPage,
      };
    },
    enabled: open && canSearch && !disabled,
  });

  const items = ouQuery.data?.items ?? [];

  const displayLabel = useMemo(() => {
    if (!value) {
      return "";
    }

    if (selectedItem?.distinguishedName === value) {
      return selectedItem.label;
    }

    return value;
  }, [selectedItem, value]);

  function handleSelect(item: AdOrganizationalUnitListItem) {
    setSelectedItem(item);
    onChange(item.distinguishedName);
    setOpen(false);
    setSearch("");
  }

  function handleClear() {
    setSelectedItem(null);
    onChange(null);
  }

  return (
    <>
      <div
        className={cn(
          "flex flex-col gap-3 rounded-lg border bg-card p-4 sm:flex-row sm:items-start sm:justify-between",
          disabled && "opacity-60",
          className,
        )}
      >
        <div className="min-w-0 flex-1 space-y-1">
          <p className="text-sm font-medium">{t(labelKey)}</p>
          <p className="text-xs text-muted-foreground">{t(descriptionKey)}</p>
          {value ? (
            <div className="min-w-0 space-y-1 pt-1">
              <p className="truncate text-sm" title={displayLabel}>
                {displayLabel}
              </p>
              <p
                className="truncate font-mono text-xs text-muted-foreground"
                title={value}
              >
                {value}
              </p>
            </div>
          ) : (
            <p className="pt-1 text-sm text-muted-foreground">{t(placeholderKey)}</p>
          )}
        </div>

        <div className="flex shrink-0 flex-wrap gap-2 sm:justify-end">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => setOpen(true)}
          >
            {value
              ? t("settings:adManagement.ouPicker.actions.change")
              : t("settings:adManagement.ouPicker.actions.select")}
          </Button>
          {allowClear && value && !disabled ? (
            <Button type="button" variant="ghost" size="sm" onClick={handleClear}>
              {t("common:actions.clear")}
            </Button>
          ) : null}
        </div>
      </div>

      <Dialog open={open}>
        <DialogContent onOpenChange={setOpen} className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{t("settings:adManagement.ouPicker.title")}</DialogTitle>
            <DialogDescription>{t("settings:adManagement.ouPicker.description")}</DialogDescription>
          </DialogHeader>
          <DialogBody>
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t("settings:adManagement.ouPicker.searchPlaceholder")}
              disabled={disabled}
              autoFocus
            />
            <div className="max-h-64 overflow-y-auto rounded-md border">
              {!canSearch ? (
                <p className="px-3 py-4 text-sm text-muted-foreground">
                  {t("settings:adManagement.ouPicker.minSearchLength")}
                </p>
              ) : null}
              {canSearch && ouQuery.isLoading ? (
                <p className="px-3 py-4 text-sm text-muted-foreground">{t("common:loading")}</p>
              ) : null}
              {canSearch && ouQuery.isError ? (
                <p className="px-3 py-4 text-sm text-destructive">
                  {t("settings:adManagement.ouPicker.loadFailed")}
                </p>
              ) : null}
              {canSearch && ouQuery.isSuccess && !items.length ? (
                <p className="px-3 py-4 text-sm text-muted-foreground">
                  {t("settings:adManagement.ouPicker.empty")}
                </p>
              ) : null}
              {canSearch && items.length > 0 ? (
                <ul className="divide-y">
                  {items.map((item) => (
                    <li key={item.distinguishedName}>
                      <button
                        type="button"
                        className={cn(
                          "w-full min-w-0 px-3 py-2 text-left hover:bg-muted/50",
                          value === item.distinguishedName && "bg-muted/40",
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
          </DialogBody>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t("common:actions.close")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

function OuListItem({ item }: { item: AdOrganizationalUnitListItem }) {
  return (
    <div className="min-w-0 space-y-0.5">
      <div className="truncate font-medium">{item.label}</div>
      <div
        className="truncate font-mono text-xs text-muted-foreground"
        title={item.distinguishedName}
      >
        {item.distinguishedName}
      </div>
    </div>
  );
}
