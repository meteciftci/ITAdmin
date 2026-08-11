import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { FormError } from "@/components/common/FormError";
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
import { getPermissionCatalog } from "@/features/permissions/api";
import { groupPermissionsByModule } from "@/features/permissions/permission-catalog";
import { getRoleById, updateRolePermissions } from "@/features/roles/api";
import type { RoleListItem } from "@/features/roles/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type AssignPermissionsDialogProps = {
  open: boolean;
  role: RoleListItem | null;
  onClose: () => void;
  onSaved: () => void;
};

export function AssignPermissionsDialog({
  open,
  role,
  onClose,
  onSaved,
}: AssignPermissionsDialogProps) {
  const { t } = useTranslation(["roles", "common", "permissions"]);
  const [selectedPermissionIdsOverride, setSelectedPermissionIdsOverride] = useState<string[] | null>(null);
  const [search, setSearch] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isSystemRole = Boolean(role?.isSystem);

  const roleDetailQuery = useQuery({
    queryKey: ["roles", "detail-for-permissions", role?.id],
    queryFn: () => getRoleById(role!.id),
    enabled: open && Boolean(role?.id),
  });

  const permissionsQuery = useQuery({
    queryKey: ["permissions", "active", "assign-dialog"],
    queryFn: () => getPermissionCatalog({ isActive: true }),
    enabled: open,
  });

  const selectedPermissionIds =
    selectedPermissionIdsOverride ??
    roleDetailQuery.data?.permissions.map((item) => item.id) ??
    [];

  const filteredPermissions = useMemo(() => {
    const source = permissionsQuery.data?.items ?? [];
    const keyword = search.trim().toLowerCase();
    if (!keyword) return source;
    return source.filter((permission) => {
      return (
        permission.name.toLowerCase().includes(keyword) ||
        permission.code.toLowerCase().includes(keyword) ||
        permission.module.toLowerCase().includes(keyword) ||
        (permission.description ?? "").toLowerCase().includes(keyword)
      );
    });
  }, [permissionsQuery.data, search]);

  const permissionGroups = useMemo(
    () => groupPermissionsByModule(filteredPermissions),
    [filteredPermissions],
  );

  const saveMutation = useMutation({
    mutationFn: () =>
      updateRolePermissions(role!.id, { permissionIds: selectedPermissionIds }),
    onSuccess: () => {
      setErrorMessage(null);
      onSaved();
      onClose();
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(error, t("roles:assignPermissions.error")),
      );
    },
  });

  const handleTogglePermission = (permissionId: string, checked: boolean) => {
    const previous = selectedPermissionIds;
    setSelectedPermissionIdsOverride(() => {
      if (checked) {
        return previous.includes(permissionId)
          ? previous
          : [...previous, permissionId];
      }

      return previous.filter((id) => id !== permissionId);
    });
  };

  const handleSave = () => {
    if (!role || isSystemRole || saveMutation.isPending) return;
    saveMutation.mutate();
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setSearch("");
      setErrorMessage(null);
      setSelectedPermissionIdsOverride(null);
      onClose();
    }
  };

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange} className="max-w-3xl">
        <DialogHeader className="space-y-2">
          <DialogTitle>{t("roles:assignPermissions.title")}</DialogTitle>
          <DialogDescription>
            {t("roles:assignPermissions.description")}
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          <FormError message={errorMessage} />

          {isSystemRole ? (
            <Alert>
              <AlertTitle>{t("roles:type.system")}</AlertTitle>
              <AlertDescription>
                {t("roles:detail.systemNotice")}
              </AlertDescription>
            </Alert>
          ) : null}

          <Input
            placeholder={t("common:actions.search")}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            disabled={isSystemRole || saveMutation.isPending}
          />

          {(roleDetailQuery.isLoading || permissionsQuery.isLoading) && (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {t("common:loading")}
            </p>
          )}

          {roleDetailQuery.isError ? (
            <FormError message={getApiErrorMessage(roleDetailQuery.error, t("common:error"))} />
          ) : null}

          {permissionsQuery.isError ? (
            <FormError
              message={getApiErrorMessage(permissionsQuery.error, t("common:error"))}
            />
          ) : null}

          {permissionGroups.length ? (
            <div className="max-h-[28rem] space-y-4 overflow-y-auto rounded-lg border bg-muted/20 p-2 sm:p-3">
              {permissionGroups.map((group) => (
                <fieldset key={group.module} className="space-y-2">
                  <legend className="sticky top-0 z-[1] w-full bg-background/95 px-2 py-1.5 text-xs font-semibold uppercase tracking-[0.06em] text-muted-foreground backdrop-blur">
                    {t(`permissions:modules.${group.module}`, {
                      defaultValue: group.module,
                    })}
                    <span className="ml-2 font-normal normal-case tracking-normal">
                      {group.items.filter((item) => selectedPermissionIds.includes(item.id)).length}/{group.items.length}
                    </span>
                  </legend>
                  {group.items.map((permission) => (
                    <label
                      key={permission.id}
                      className="flex cursor-pointer items-start gap-3 rounded-lg border bg-card p-3 text-sm transition-colors hover:bg-muted/40 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
                    >
                      <Checkbox
                        checked={selectedPermissionIds.includes(permission.id)}
                        onChange={(event) =>
                          handleTogglePermission(permission.id, event.target.checked)
                        }
                        disabled={isSystemRole || saveMutation.isPending}
                      />
                      <span className="min-w-0">
                        <span className="block font-medium">{permission.name}</span>
                        <span className="block break-all font-mono text-xs text-muted-foreground">
                          {permission.code}
                        </span>
                        <span className="mt-1 block text-xs leading-5 text-muted-foreground">
                          {permission.description || t("permissions:item.noDescription")}
                        </span>
                      </span>
                    </label>
                  ))}
                </fieldset>
              ))}
            </div>
          ) : null}

          {permissionsQuery.isSuccess && !filteredPermissions.length ? (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {t("roles:assignPermissions.noPermissions")}
            </p>
          ) : null}
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={handleSave}
            disabled={
              isSystemRole ||
              saveMutation.isPending ||
              roleDetailQuery.isLoading ||
              permissionsQuery.isLoading
            }
          >
            {saveMutation.isPending ? t("roles:assignPermissions.saving") : t("roles:assignPermissions.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
