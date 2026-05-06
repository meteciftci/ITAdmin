import { useMemo, useState } from "react";
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
import { getRoles, updateUserRoles } from "@/features/users/api";
import type { RoleListItem, UserListItem } from "@/features/users/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type AssignRolesDialogProps = {
  open: boolean;
  user: UserListItem | null;
  onOpenChange: (open: boolean) => void;
  onUpdated: () => void;
};

export function AssignRolesDialog({
  open,
  user,
  onOpenChange,
  onUpdated,
}: AssignRolesDialogProps) {
  const { t } = useTranslation(["users", "common"]);
  const [selectedRoleIdsOverride, setSelectedRoleIdsOverride] = useState<string[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["roles", "active-for-user-assign", open, user?.id],
    queryFn: () => getRoles({ isActive: true, pageSize: 100 }),
    enabled: open,
  });

  const initialSelectedRoleIds = useMemo(() => {
    const roleIdsByCode = new Map<string, string>();
    (rolesQuery.data?.items ?? []).forEach((role) => {
      roleIdsByCode.set(role.code, role.id);
    });
    return (user?.roles ?? [])
      .map((code) => roleIdsByCode.get(code))
      .filter((id): id is string => Boolean(id));
  }, [rolesQuery.data, user?.roles]);

  const selectedRoleIds = selectedRoleIdsOverride ?? initialSelectedRoleIds;

  const updateRolesMutation = useMutation({
    mutationFn: (roleIds: string[]) => {
      if (!user) throw new Error("No selected user");
      return updateUserRoles(user.id, { roleIds });
    },
    onSuccess: () => {
      setErrorMessage(null);
      onUpdated();
      onOpenChange(false);
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(error, t("users:assignRoles.error")),
      );
    },
  });

  const handleToggleRole = (role: RoleListItem, checked: boolean) => {
    const previous = selectedRoleIds;
    setSelectedRoleIdsOverride(() => {
      if (checked) {
        return previous.includes(role.id) ? previous : [...previous, role.id];
      }
      return previous.filter((id) => id !== role.id);
    });
  };

  const handleSave = () => {
    if (!user) return;
    updateRolesMutation.mutate(selectedRoleIds);
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setErrorMessage(null);
      setSelectedRoleIdsOverride(null);
    }
    onOpenChange(next);
  };

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange} className="max-w-2xl">
        <DialogHeader className="space-y-2">
          <DialogTitle>{t("users:assignRoles.title")}</DialogTitle>
          <DialogDescription>{t("users:assignRoles.description")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4">
          <FormError message={errorMessage} />

          {rolesQuery.isLoading ? (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {t("common:loading")}
            </p>
          ) : null}

          {rolesQuery.isError ? (
            <FormError
              message={getApiErrorMessage(rolesQuery.error, t("users:assignRoles.noRoles"))}
            />
          ) : null}

          {rolesQuery.data?.items.length ? (
            <div className="grid max-h-[50vh] gap-2 overflow-y-auto rounded-lg border p-2 md:grid-cols-2">
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

          {rolesQuery.isSuccess && !rolesQuery.data.items.length ? (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {t("users:assignRoles.noRoles")}
            </p>
          ) : null}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
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
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
