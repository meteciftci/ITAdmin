import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { createUser, lookupDirectoryUsers } from "@/features/users/api";
import { useTranslation } from "react-i18next";

type AddUserDialogProps = {
  onClose: () => void;
  onCreated: () => void;
};

type ApiErrorPayload = {
  message?: string;
};

const getErrorMessage = (error: unknown, fallback: string): string => {
  const apiError = error as AxiosError<ApiErrorPayload>;
  return apiError.response?.data?.message ?? fallback;
};

export function AddUserDialog({ onClose, onCreated }: AddUserDialogProps) {
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

  const createUserMutation = useMutation({
    mutationFn: createUser,
    onSuccess: () => {
      setErrorMessage(null);
      onCreated();
      onClose();
    },
    onError: (error) => {
      setErrorMessage(getErrorMessage(error, t("users:add.error")));
    },
  });

  const handleCreate = (directoryObjectId: string) => {
    setSelectedDirectoryId(directoryObjectId);
    createUserMutation.mutate({ directoryObjectId, isActive: true });
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle>{t("users:add.title")}</CardTitle>
        <Button variant="ghost" onClick={onClose}>
          {t("common:actions.close")}
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {errorMessage ? (
          <Alert variant="destructive">
            <AlertTitle>{t("common:error")}</AlertTitle>
            <AlertDescription>{errorMessage}</AlertDescription>
          </Alert>
        ) : null}
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

        {debouncedSearch.length < 2 ? (
          <p className="text-sm text-muted-foreground">
            {t("users:add.description")}
          </p>
        ) : null}

        {lookupQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
        ) : null}

        {lookupQuery.isError ? (
          <Alert variant="destructive">
            <AlertTitle>{t("common:error")}</AlertTitle>
            <AlertDescription>
              {getErrorMessage(
                lookupQuery.error,
                t("common:error"),
              )}
            </AlertDescription>
          </Alert>
        ) : null}

        {lookupQuery.data?.items.length ? (
          <div className="max-h-72 space-y-2 overflow-y-auto">
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
      </CardContent>
    </Card>
  );
}
