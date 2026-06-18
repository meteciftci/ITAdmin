import axios from "axios";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
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
import { searchSetupOrganizationalUnits } from "@/features/setup/api";
import {
  buildCompleteSetupLdapPayload,
  isOuSearchBelowMinLength,
  shouldFetchOuSearchResults,
  type SetupLdapFormValues,
  type SetupOuSelection,
} from "@/features/setup/setup-form";
import { getApiErrorMessage } from "@/lib/api-error";

type SetupOuPickerProps = {
  id: string;
  label: string;
  value: SetupOuSelection | null;
  onChange: (value: SetupOuSelection | null) => void;
  setupKey: string;
  ldap: SetupLdapFormValues;
  disabled?: boolean;
  required?: boolean;
};

export function SetupOuPicker({
  id,
  label,
  value,
  onChange,
  setupKey,
  ldap,
  disabled = false,
  required = false,
}: SetupOuPickerProps) {
  const { t } = useTranslation(["setup", "common"]);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [items, setItems] = useState<SetupOuSelection[]>([]);
  const [hasMore, setHasMore] = useState(false);

  const loadResults = useCallback(
    async (searchTerm: string) => {
      setIsLoading(true);
      setErrorMessage(null);
      setHasMore(false);
      try {
        const response = await searchSetupOrganizationalUnits({
          setupKey,
          ldap: buildCompleteSetupLdapPayload(ldap),
          search: searchTerm.trim().length === 0 ? null : searchTerm.trim(),
          parentDistinguishedName: null,
        });

        setItems(
          response.items.map((item) => ({
            distinguishedName: item.distinguishedName,
            label: item.label,
          })),
        );
        setHasMore(response.hasMore);
      } catch (error) {
        const fallback = t("setup:ouPicker.errors.searchFailed");
        setErrorMessage(axios.isAxiosError(error) ? getApiErrorMessage(error, fallback) : fallback);
        setItems([]);
        setHasMore(false);
      } finally {
        setIsLoading(false);
      }
    },
    [ldap, setupKey, t],
  );

  useEffect(() => {
    if (!shouldFetchOuSearchResults(open, search)) {
      return;
    }

    const trimmed = search.trim();
    const timeoutId = window.setTimeout(() => {
      void loadResults(trimmed);
    }, 300);

    return () => window.clearTimeout(timeoutId);
  }, [loadResults, open, search]);

  const handleSearchChange = (nextSearch: string) => {
    setSearch(nextSearch);
    setHasMore(false);
    if (isOuSearchBelowMinLength(nextSearch)) {
      setItems([]);
      setErrorMessage(null);
    }
  };

  const handleOpen = () => {
    if (disabled) {
      return;
    }

    setOpen(true);
    setSearch("");
    setItems([]);
    setErrorMessage(null);
    setHasMore(false);
  };

  const handleSelect = (selection: SetupOuSelection) => {
    onChange(selection);
    setOpen(false);
  };

  const handleClear = () => {
    onChange(null);
  };

  const trimmedSearch = search.trim();
  const showShortSearchEmptyState =
    isOuSearchBelowMinLength(search) && !isLoading && !errorMessage;

  return (
    <div className="space-y-2">
      <Label htmlFor={id}>
        {label}
        {required ? " *" : ""}
      </Label>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
        <div className="min-w-0 flex-1 rounded-lg border bg-muted/20 px-3 py-2">
          {value ? (
            <div className="space-y-1">
              <p className="text-sm font-medium">{value.label}</p>
              <p className="truncate font-mono text-xs text-muted-foreground" title={value.distinguishedName}>
                {value.distinguishedName}
              </p>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t("setup:ouPicker.noSelection")}</p>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          {!required && value ? (
            <Button type="button" variant="outline" onClick={handleClear} disabled={disabled}>
              {t("setup:actions.clear")}
            </Button>
          ) : null}
          <Button type="button" variant="outline" onClick={handleOpen} disabled={disabled}>
            {t("setup:actions.browse")}
          </Button>
        </div>
      </div>

      <Dialog open={open}>
        <DialogContent className="max-w-xl" onOpenChange={setOpen}>
          <DialogHeader>
            <DialogTitle>{t("setup:ouPicker.title")}</DialogTitle>
            <DialogDescription>{t("setup:ouPicker.description")}</DialogDescription>
          </DialogHeader>
          <DialogBody className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor={`${id}-search`}>{t("setup:actions.search")}</Label>
              <Input
                id={`${id}-search`}
                value={search}
                onChange={(event) => handleSearchChange(event.target.value)}
                placeholder={t("setup:ouPicker.searchPlaceholder")}
                autoComplete="off"
              />
              <p className="text-xs text-muted-foreground">{t("setup:ouPicker.minSearchLength")}</p>
            </div>

            {isLoading ? <LoadingState text={t("setup:ouPicker.loading")} /> : null}

            {!isLoading && errorMessage ? (
              <ErrorState
                title={t("setup:ouPicker.errors.title")}
                description={errorMessage}
                retry={
                  shouldFetchOuSearchResults(true, search) ? (
                    <Button type="button" variant="outline" size="sm" onClick={() => void loadResults(trimmedSearch)}>
                      {t("setup:actions.retry")}
                    </Button>
                  ) : null
                }
              />
            ) : null}

            {showShortSearchEmptyState ? (
              <EmptyState
                title={t("setup:ouPicker.emptyTitle")}
                description={t("setup:ouPicker.minSearchLength")}
              />
            ) : null}

            {!isLoading && !errorMessage && !showShortSearchEmptyState && items.length === 0 ? (
              <EmptyState
                title={t("setup:ouPicker.emptyTitle")}
                description={t("setup:ouPicker.emptyDescription")}
              />
            ) : null}

            {!isLoading && !errorMessage && items.length > 0 ? (
              <div className="space-y-2">
                <ul className="max-h-72 space-y-2 overflow-y-auto">
                  {items.map((item) => (
                    <li key={item.distinguishedName}>
                      <button
                        type="button"
                        className="w-full rounded-lg border px-3 py-2 text-left hover:bg-muted/30"
                        onClick={() => handleSelect(item)}
                      >
                        <p className="text-sm font-medium">{item.label}</p>
                        <p className="truncate font-mono text-xs text-muted-foreground">{item.distinguishedName}</p>
                      </button>
                    </li>
                  ))}
                </ul>
                {hasMore ? (
                  <p className="text-xs text-muted-foreground">{t("setup:ouPicker.hasMore")}</p>
                ) : null}
              </div>
            ) : null}
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
