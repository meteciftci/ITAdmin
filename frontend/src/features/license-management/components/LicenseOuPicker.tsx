import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, X } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  AD_COMBOBOX_POPOVER_CONTENT_PROPS,
  AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME,
  AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME,
  AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME,
} from "@/features/ad-management/ad-combobox-styles";
import { searchDirectoryOrganizationalUnits } from "@/features/license-management/api";
import type { DirectoryOrganizationalUnitLookupItem } from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

const MIN_SEARCH_LENGTH = 2;

export type LicenseRequestOuSnapshot = {
  objectGuid: string;
  displayName: string;
  distinguishedName: string;
};

type Props = {
  value: LicenseRequestOuSnapshot | null;
  onChange: (value: LicenseRequestOuSnapshot | null) => void;
  disabled?: boolean;
  label?: string;
  placeholder?: string;
};

function formatOuPrimaryLabel(item: DirectoryOrganizationalUnitLookupItem): string {
  return item.displayName || item.name || item.distinguishedName;
}

export function LicenseOuPicker({
  value,
  onChange,
  disabled,
  label,
  placeholder,
}: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const ouQuery = useQuery({
    queryKey: ["license-management", "directory-organizational-units", "search", normalizedSearch],
    queryFn: () => searchDirectoryOrganizationalUnits(normalizedSearch),
    enabled: open && canSearch && !disabled,
  });

  const items = ouQuery.data?.items ?? [];

  const triggerLabel = value ? value.displayName : placeholder ?? t("licenseManagement:requests.placeholders.selectUnit");

  return (
    <div className="space-y-2">
      {label ? <Label>{label}</Label> : null}

      {value ? (
        <div className="flex items-start justify-between gap-3 rounded-lg border bg-muted/20 p-3">
          <div className="min-w-0 space-y-1">
            <p className="text-sm font-medium">{value.displayName}</p>
            <p className="truncate text-xs text-muted-foreground" title={value.distinguishedName}>
              {value.distinguishedName}
            </p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="shrink-0"
            disabled={disabled}
            onClick={() => onChange(null)}
            aria-label={t("common:actions.clear")}
          >
            <X className="size-4" />
          </Button>
        </div>
      ) : (
        <Popover open={open} onOpenChange={setOpen}>
          <div className={AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME}>
            <PopoverTrigger asChild>
              <button
                type="button"
                disabled={disabled}
                className={AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME}
              >
                <span className={AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME}>{triggerLabel}</span>
                <ChevronDown className="size-4 shrink-0 opacity-60" />
              </button>
            </PopoverTrigger>
          </div>
          <PopoverContent {...AD_COMBOBOX_POPOVER_CONTENT_PROPS}>
            <div className="space-y-2">
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t("licenseManagement:requests.placeholders.searchUnit")}
                autoFocus
              />
              <div className="max-h-60 overflow-y-auto">
                {!canSearch ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">
                    {t("common:select.searchOptions")}
                  </p>
                ) : null}
                {canSearch && ouQuery.isLoading ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:dataTable.loading")}</p>
                ) : null}
                {canSearch && ouQuery.isError ? (
                  <p className="px-2 py-3 text-sm text-destructive">{t("common:messages.operationFailed")}</p>
                ) : null}
                {canSearch && !ouQuery.isLoading && !ouQuery.isError && items.length === 0 ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:select.noOptions")}</p>
                ) : null}
                {items.map((item) => (
                  <button
                    key={item.objectGuid}
                    type="button"
                    className="flex w-full flex-col items-start gap-0.5 rounded-md px-2 py-2 text-left hover:bg-muted"
                    onClick={() => {
                      onChange({
                        objectGuid: item.objectGuid,
                        displayName: formatOuPrimaryLabel(item),
                        distinguishedName: item.distinguishedName,
                      });
                      setOpen(false);
                      setSearch("");
                    }}
                  >
                    <span className="text-sm font-medium">{formatOuPrimaryLabel(item)}</span>
                    <span className="truncate text-xs text-muted-foreground" title={item.distinguishedName}>
                      {item.distinguishedName}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          </PopoverContent>
        </Popover>
      )}
    </div>
  );
}
