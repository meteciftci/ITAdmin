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
import { Label } from "@/components/ui/label";
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

const MIN_SEARCH_LENGTH = 2;

type OuSearchContext = "users" | "groups" | "computers" | "manage";

type Props = {
  value: string | null;
  onChange: (distinguishedName: string | null) => void;
  disabled?: boolean;
  className?: string;
  showFieldLabel?: boolean;
  searchContext?: OuSearchContext;
  allowClear?: boolean;
  required?: boolean;
  fieldLabelKey?: string;
  placeholderKey?: string;
  searchKey?: string;
  emptyKey?: string;
  errorKey?: string;
  excludeDistinguishedName?: string | null;
};

export function AdOuPickerField({
  value,
  onChange,
  disabled,
  className,
  showFieldLabel = true,
  searchContext = "users",
  allowClear = false,
  required = true,
  fieldLabelKey = "adManagement:users.create.fields.ou",
  placeholderKey = "adManagement:users.create.fields.ouPlaceholder",
  searchKey = "adManagement:users.create.fields.ouSearch",
  emptyKey = "adManagement:users.create.empty.ouNotFound",
  errorKey = "adManagement:users.create.errors.ouLoadFailed",
  excludeDistinguishedName = null,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "settings"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<AdOrganizationalUnitListItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const ouQuery = useQuery({
    queryKey: ["ad-management", "organizational-units", searchContext, normalizedSearch],
    queryFn: async () => {
      const params = {
        search: normalizedSearch,
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
    enabled: open && canSearch && !disabled,
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
    <div className={cn("w-full min-w-0 space-y-1.5", className)}>
      {showFieldLabel ? (
        <Label>
          {t(fieldLabelKey)}
          {required ? " *" : ""}
        </Label>
      ) : null}

      <div className="rounded-lg border bg-muted/10 p-3">
        {value ? (
          <div className="min-w-0 space-y-1">
            <p className="truncate text-sm font-medium" title={displayLabel}>
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
          <p className="text-sm text-muted-foreground">{t(placeholderKey)}</p>
        )}

        <div className="mt-3 flex flex-wrap gap-2">
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
              placeholder={t(searchKey)}
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
                <p className="px-3 py-4 text-sm text-destructive">{t(errorKey)}</p>
              ) : null}
              {canSearch && ouQuery.isSuccess && !items.length ? (
                <p className="px-3 py-4 text-sm text-muted-foreground">{t(emptyKey)}</p>
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
    </div>
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
