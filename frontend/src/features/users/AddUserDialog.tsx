import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { createUser, lookupDirectoryUsers } from "@/features/users/api";

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
      setErrorMessage(getErrorMessage(error, "User could not be created."));
    },
  });

  const handleCreate = (directoryObjectId: string) => {
    setSelectedDirectoryId(directoryObjectId);
    createUserMutation.mutate({ directoryObjectId, isActive: true });
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle>Add User</CardTitle>
        <Button variant="ghost" onClick={onClose}>
          Close
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {errorMessage ? (
          <Alert variant="destructive">
            <AlertTitle>Operation Failed</AlertTitle>
            <AlertDescription>{errorMessage}</AlertDescription>
          </Alert>
        ) : null}
        <div className="space-y-2">
          <Input
            value={searchValue}
            onChange={(event) => setSearchValue(event.target.value)}
            placeholder="Search directory user by name, username or email"
          />
          <p className="text-xs text-muted-foreground">
            Enter at least 2 characters to search directory users.
          </p>
        </div>

        {debouncedSearch.length < 2 ? (
          <p className="text-sm text-muted-foreground">
            Start typing to search from directory.
          </p>
        ) : null}

        {lookupQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">Searching directory...</p>
        ) : null}

        {lookupQuery.isError ? (
          <Alert variant="destructive">
            <AlertTitle>Lookup Failed</AlertTitle>
            <AlertDescription>
              {getErrorMessage(
                lookupQuery.error,
                "Directory lookup could not be completed.",
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
                    National ID: {item.nationalIdMasked || "-"}
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-xs text-muted-foreground">
                      {item.isAlreadyPortalUser ? "Already added" : "Not in portal yet"}
                    </span>
                    <Button
                      variant="outline"
                      disabled={disabled || createUserMutation.isPending}
                      onClick={() => handleCreate(item.directoryObjectId)}
                    >
                      {isBusy ? "Adding..." : "Add"}
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        ) : null}

        {lookupQuery.isSuccess && !lookupQuery.data.items.length ? (
          <p className="text-sm text-muted-foreground">No directory users found.</p>
        ) : null}
      </CardContent>
    </Card>
  );
}
