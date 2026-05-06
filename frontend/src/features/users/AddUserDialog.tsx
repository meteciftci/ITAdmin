import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";

import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { createUser, lookupDirectoryUsers } from "@/features/users/api";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type AddUserDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
};

export function AddUserDialog({ open, onOpenChange, onCreated }: AddUserDialogProps) {
  const { t } = useTranslation(["users", "common"]);
  const [searchValue, setSearchValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedDirectoryId, setSelectedDirectoryId] = useState<string | null>(
    null,
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(searchValue.trim());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [searchValue]);

  const lookupQuery = useQuery({
    queryKey: ["users", "lookup-directory", debouncedSearch],
    queryFn: () =>
      lookupDirectoryUsers({
        search: debouncedSearch,
        maxResults: 20,
      }),
    enabled: debouncedSearch.length >= 2,
  });

  function handleOpenChange(next: boolean) {
    if (!next) {
      setSearchValue("");
      setDebouncedSearch("");
      setSelectedDirectoryId(null);
      setErrorMessage(null);
    }
    onOpenChange(next);
  }

  const createUserMutation = useMutation({
    mutationFn: createUser,
    onSuccess: () => {
      setErrorMessage(null);
      onCreated();
      handleOpenChange(false);
    },
    onError: (error) => {
      setErrorMessage(getApiErrorMessage(error, t("users:add.error")));
    },
  });

  const handleCreate = (directoryObjectId: string) => {
    setSelectedDirectoryId(directoryObjectId);
    createUserMutation.mutate({ directoryObjectId, isActive: true });
  };

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange} className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("users:add.title")}</DialogTitle>
          <DialogDescription>{t("users:add.description")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4">
          <FormError message={errorMessage} />
          <div className="space-y-2">
            <Input
              value={searchValue}
              onChange={(event) => setSearchValue(event.target.value)}
              placeholder={t("users:add.searchPlaceholder")}
            />
            <p className="text-xs text-muted-foreground">
              {t("users:add.minSearch")}
            </p>
          </div>

          {lookupQuery.isLoading ? (
            <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
          ) : null}

          {lookupQuery.isError ? (
            <FormError message={getApiErrorMessage(lookupQuery.error, t("common:error"))} />
          ) : null}

          {lookupQuery.data?.items.length ? (
            <div className="max-h-[50vh] space-y-2 overflow-y-auto">
              {lookupQuery.data.items.map((item) => {
                const disabled = item.isAlreadyPortalUser;
                const isBusy =
                  createUserMutation.isPending &&
                  selectedDirectoryId === item.directoryObjectId;

                return (
                  <div
                    key={item.directoryObjectId}
                    className="rounded-lg border p-3 text-sm"
                  >
                    <div className="font-medium">{item.displayName}</div>
                    <div className="text-muted-foreground">{item.userName}</div>
                    <div className="text-muted-foreground">{item.email || "-"}</div>
                    <div className="text-muted-foreground">
                      {t("users:table.nationalIdMasked")}: {item.nationalIdMasked || "-"}
                    </div>
                    <div className="mt-2 flex items-center justify-between">
                      <span className="text-xs text-muted-foreground">
                        {item.isAlreadyPortalUser
                          ? t("users:add.alreadyAdded")
                          : t("users:add.notInPortalYet")}
                      </span>
                      <Button
                        variant="outline"
                        disabled={disabled || createUserMutation.isPending}
                        onClick={() => handleCreate(item.directoryObjectId)}
                      >
                        {isBusy ? t("users:add.creating") : t("users:add.create")}
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : null}

          {lookupQuery.isSuccess && !lookupQuery.data.items.length ? (
            <p className="text-sm text-muted-foreground">{t("users:add.noResults")}</p>
          ) : null}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            {t("common:actions.close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
