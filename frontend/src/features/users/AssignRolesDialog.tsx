import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getRoles, updateUserRoles } from "@/features/users/api";
import type { RoleListItem } from "@/features/users/types";

type AssignRolesDialogProps = {
  userId: string;
  currentRoleCodes: string[];
  onClose: () => void;
  onSaved: () => void;
};

type ApiErrorPayload = {
  message?: string;
};

const getErrorMessage = (error: unknown, fallback: string): string => {
  const apiError = error as AxiosError<ApiErrorPayload>;
  return apiError.response?.data?.message ?? fallback;
};

export function AssignRolesDialog({
  userId,
  currentRoleCodes,
  onClose,
  onSaved,
}: AssignRolesDialogProps) {
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["roles", "active-for-user-assign"],
    queryFn: () => getRoles({ isActive: true, pageSize: 100 }),
  });

  const roleIdsByCode = useMemo(() => {
    const map = new Map<string, string>();
    (rolesQuery.data?.items ?? []).forEach((role) => {
      map.set(role.code, role.id);
    });
    return map;
  }, [rolesQuery.data]);

  useEffect(() => {
    if (!rolesQuery.data) return;
    const initialIds = currentRoleCodes
      .map((code) => roleIdsByCode.get(code))
      .filter((id): id is string => Boolean(id));
    setSelectedRoleIds(initialIds);
  }, [currentRoleCodes, roleIdsByCode, rolesQuery.data]);

  const updateRolesMutation = useMutation({
    mutationFn: (roleIds: string[]) => updateUserRoles(userId, { roleIds }),
    onSuccess: () => {
      setErrorMessage(null);
      onSaved();
      onClose();
    },
    onError: (error) => {
      setErrorMessage(
        getErrorMessage(error, "User roles could not be updated."),
      );
    },
  });

  const handleToggleRole = (role: RoleListItem, checked: boolean) => {
    setSelectedRoleIds((previous) => {
      if (checked) {
        return previous.includes(role.id) ? previous : [...previous, role.id];
      }
      return previous.filter((id) => id !== role.id);
    });
  };

  const handleSave = () => {
    updateRolesMutation.mutate(selectedRoleIds);
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle>Assign Roles</CardTitle>
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

        {rolesQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">Loading roles...</p>
        ) : null}

        {rolesQuery.isError ? (
          <Alert variant="destructive">
            <AlertTitle>Roles Could Not Be Loaded</AlertTitle>
            <AlertDescription>
              {getErrorMessage(rolesQuery.error, "Could not fetch active roles.")}
            </AlertDescription>
          </Alert>
        ) : null}

        {rolesQuery.data?.items.length ? (
          <div className="grid gap-2 md:grid-cols-2">
            {rolesQuery.data.items.map((role) => (
              <label
                key={role.id}
                className="flex items-start gap-2 rounded-lg border p-2 text-sm"
              >
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={selectedRoleIds.includes(role.id)}
                  onChange={(event) => handleToggleRole(role, event.target.checked)}
                />
                <span>
                  <span className="block font-medium">{role.name}</span>
                  <span className="block text-xs text-muted-foreground">
                    {role.code} | permissions: {role.permissionCount}
                  </span>
                </span>
              </label>
            ))}
          </div>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={updateRolesMutation.isPending || rolesQuery.isLoading}
          >
            {updateRolesMutation.isPending ? "Saving..." : "Save"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
