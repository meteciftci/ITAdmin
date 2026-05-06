import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getRoles, updateUserRoles } from "@/features/users/api";
import type { RoleListItem } from "@/features/users/types";
import { useTranslation } from "react-i18next";

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
  const { t } = useTranslation(["users", "common"]);
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
        getErrorMessage(error, t("users:assignRoles.error")),
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
        <CardTitle>{t("users:assignRoles.title")}</CardTitle>
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

        {rolesQuery.isLoading ? (
          <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
        ) : null}

        {rolesQuery.isError ? (
          <Alert variant="destructive">
            <AlertTitle>{t("users:assignRoles.noRoles")}</AlertTitle>
            <AlertDescription>
              {getErrorMessage(rolesQuery.error, t("users:assignRoles.noRoles"))}
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
                    {role.code} | {role.permissionCount}
                  </span>
                </span>
              </label>
            ))}
          </div>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={handleSave}
            disabled={updateRolesMutation.isPending || rolesQuery.isLoading}
          >
            {updateRolesMutation.isPending
              ? t("users:assignRoles.saving")
              : t("users:assignRoles.save")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
