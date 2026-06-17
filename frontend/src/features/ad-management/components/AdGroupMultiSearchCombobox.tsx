import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  AD_COMBOBOX_POPOVER_CONTENT_PROPS,
  AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME,
  AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME,
  AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME,
} from "@/features/ad-management/ad-combobox-styles";
import {
  formatAdGroupSelectionPrimaryLabel,
  formatAdGroupSelectionSecondaryLabel,
} from "@/features/ad-management/ad-group-display";
import { searchAdGroups } from "@/features/ad-management/api";
import type { AdGroupSearchItem } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

const MIN_SEARCH_LENGTH = 2;

type Props = {
  selectedItems: AdGroupSearchItem[];
  onSelectedItemsChange: (items: AdGroupSearchItem[]) => void;
  disabledGroupDns: ReadonlySet<string>;
  disabled?: boolean;
};

export function AdGroupMultiSearchCombobox({
  selectedItems,
  onSelectedItemsChange,
  disabledGroupDns,
  disabled,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 350);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;

  const selectedDns = useMemo(
    () => new Set(selectedItems.map((item) => item.distinguishedName)),
    [selectedItems],
  );

  const groupsQuery = useQuery({
    queryKey: ["ad-management", "groups", "search", normalizedSearch],
    queryFn: () => searchAdGroups(normalizedSearch),
    enabled: open && canSearch && !disabled,
  });

  const items = useMemo(() => groupsQuery.data?.items ?? [], [groupsQuery.data]);

  const triggerLabel = useMemo(() => {
    if (selectedItems.length === 0) {
      return "";
    }

    return t("adManagement:membershipMultiSelect.groupsSelectedCount", {
      count: selectedItems.length,
    });
  }, [selectedItems.length, t]);

  function handleSelect(item: AdGroupSearchItem) {
    if (disabledGroupDns.has(item.distinguishedName) || selectedDns.has(item.distinguishedName)) {
      return;
    }

    onSelectedItemsChange([...selectedItems, item]);
    setSearch("");
  }

  return (
    <div className="space-y-1.5">
      <Label>{t("adManagement:users.groups.fields.searchGroup")}</Label>
      <Popover open={open} onOpenChange={setOpen}>
        <div className={AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME}>
          <PopoverTrigger asChild>
            <button
              type="button"
              disabled={disabled}
              className={AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME}
            >
              <span
                className={cn(
                  AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME,
                  !triggerLabel && "text-muted-foreground",
                )}
              >
                {triggerLabel || t("adManagement:users.groups.fields.selectGroup")}
              </span>
              <ChevronDown className="ml-2 size-4 shrink-0 opacity-60" />
            </button>
          </PopoverTrigger>
        </div>
        <PopoverContent {...AD_COMBOBOX_POPOVER_CONTENT_PROPS}>
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("adManagement:users.groups.fields.searchGroupPlaceholder")}
            disabled={disabled}
            autoFocus
          />
          <div className="mt-2 max-h-56 overflow-y-auto">
            {!canSearch ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:users.groups.empty.searchMinLength")}
              </p>
            ) : null}
            {canSearch && groupsQuery.isLoading ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">{t("common:loading")}</p>
            ) : null}
            {canSearch && !groupsQuery.isLoading && items.length === 0 ? (
              <p className="px-2 py-3 text-sm text-muted-foreground">
                {t("adManagement:users.groups.empty.groupNotFound")}
              </p>
            ) : null}
            {canSearch && !groupsQuery.isLoading
              ? items.map((item) => {
                  const isMember = disabledGroupDns.has(item.distinguishedName);
                  const isSelected = selectedDns.has(item.distinguishedName);
                  const isDisabled = isMember || isSelected;
                  const primaryLabel = formatAdGroupSelectionPrimaryLabel(item);
                  const secondaryLabel = formatAdGroupSelectionSecondaryLabel(item);

                  return (
                    <button
                      key={item.distinguishedName}
                      type="button"
                      disabled={isDisabled}
                      onClick={() => handleSelect(item)}
                      className={cn(
                        "flex w-full min-w-0 flex-col gap-0.5 rounded-md px-2 py-2 text-left text-sm",
                        isDisabled
                          ? "cursor-not-allowed opacity-50"
                          : "hover:bg-muted/60",
                      )}
                    >
                      <span className="truncate font-medium">{primaryLabel}</span>
                      {secondaryLabel ? (
                        <span className="truncate text-xs text-muted-foreground">
                          {secondaryLabel}
                        </span>
                      ) : null}
                      {item.description ? (
                        <span className="truncate text-xs text-muted-foreground">
                          {item.description}
                        </span>
                      ) : null}
                      <span
                        className="truncate font-mono text-xs text-muted-foreground"
                        title={item.distinguishedName}
                      >
                        {item.distinguishedName}
                      </span>
                      {isMember ? (
                        <span className="text-xs text-muted-foreground">
                          {t("adManagement:membershipMultiSelect.alreadyDirectGroupMember")}
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
