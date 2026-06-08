import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { FormError } from "@/components/common/FormError";
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
import { getRoles, getUserById, updateUserRoles } from "@/features/users/api";
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
  const queryClient = useQueryClient();
  const [selectedRoleIdsOverride, setSelectedRoleIdsOverride] = useState<string[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["roles", "active-for-user-assign", open, user?.id],
    queryFn: () => getRoles({ isActive: true, pageSize: 100 }),
    enabled: open,
  });

  const userDetailQuery = useQuery({
    queryKey: ["users", "detail", user?.id],
    queryFn: () => getUserById(user!.id),
    enabled: open && Boolean(user?.id),
  });

  const updateRolesMutation = useMutation({
    mutationFn: (roleIds: string[]) => {
      if (!user) throw new Error("No selected user");
      return updateUserRoles(user.id, { roleIds });
    },
    onSuccess: () => {
      setErrorMessage(null);
      queryClient.invalidateQueries({ queryKey: ["users", "list"] });
      if (user) {
        queryClient.invalidateQueries({ queryKey: ["users", "detail", user.id] });
      }
      onUpdated();
      onOpenChange(false);
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(error, t("users:assignRoles.error")),
      );
    },
  });

  const detailMatchesUser =
    Boolean(user?.id && userDetailQuery.data?.id === user.id);

  const userRolesResolved = detailMatchesUser || userDetailQuery.isError;

  const initialSelectedRoleIds = useMemo(() => {
    const roleIdsByCode = new Map<string, string>();
    (rolesQuery.data?.items ?? []).forEach((role) => {
      roleIdsByCode.set(role.code, role.id);
    });

    const detail = userDetailQuery.data;

    let roleCodes: string[] = [];
    if (detail && user?.id && detail.id === user.id) {
      roleCodes = detail.roles;
    } else if (userDetailQuery.isError) {
      roleCodes = user?.roles ?? [];
    }

    return roleCodes
      .map((code) => roleIdsByCode.get(code))
      .filter((id): id is string => Boolean(id));
  }, [rolesQuery.data, user, userDetailQuery.data, userDetailQuery.isError]);

  const selectedRoleIds = selectedRoleIdsOverride ?? initialSelectedRoleIds;

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

  const displayLabel = user?.displayName?.trim() || user?.userName || "";
  const dialogTitle = displayLabel
    ? t("users:assignRoles.titleWithUser", { name: displayLabel })
    : t("users:assignRoles.title");

  const showRolesLoadingOverlay =
    Boolean(open && user && !userRolesResolved && userDetailQuery.isPending);

  const saveDisabled =
    updateRolesMutation.isPending ||
    rolesQuery.isLoading ||
    !userRolesResolved;

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange} className="max-w-2xl">
        <DialogHeader className="space-y-2">
          <DialogTitle>{dialogTitle}</DialogTitle>
          <DialogDescription>{t("users:assignRoles.description")}</DialogDescription>
        </DialogHeader>
        <DialogBody>
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
            <div className="relative">
              {showRolesLoadingOverlay ? (
                <div className="absolute inset-0 z-10 flex items-center justify-center rounded-lg border bg-background/80">
                  <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
                </div>
              ) : null}
              <div
                className={`grid max-h-[50vh] gap-2 overflow-y-auto rounded-lg border p-2 md:grid-cols-2 ${showRolesLoadingOverlay ? "pointer-events-none opacity-50" : ""}`}
              >
                {rolesQuery.data.items.map((role) => (
                  <label
                    key={role.id}
                    className="flex items-start gap-2 rounded-lg border p-2 text-sm"
                  >
                    <input
                      type="checkbox"
                      className="mt-0.5"
                      disabled={!userRolesResolved}
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
            </div>
          ) : null}

          {rolesQuery.isSuccess && !rolesQuery.data.items.length ? (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {t("users:assignRoles.noRoles")}
            </p>
          ) : null}
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={handleSave}
            disabled={saveDisabled}
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
