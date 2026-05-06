import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { FormError } from "@/components/common/FormError";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { getPermissions, getRoleById, updateRolePermissions } from "@/features/roles/api";
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
  const { t } = useTranslation(["roles", "common"]);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState<string[]>([]);
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
    queryFn: () => getPermissions({ isActive: true, pageSize: 100 }),
    enabled: open,
  });

  useEffect(() => {
    if (!roleDetailQuery.data) return;
    setSelectedPermissionIds(roleDetailQuery.data.permissions.map((item) => item.id));
  }, [roleDetailQuery.data]);

  const filteredPermissions = useMemo(() => {
    const source = permissionsQuery.data?.items ?? [];
    const keyword = search.trim().toLowerCase();
    if (!keyword) return source;
    return source.filter((permission) => {
      return (
        permission.name.toLowerCase().includes(keyword) ||
        permission.code.toLowerCase().includes(keyword) ||
        (permission.description ?? "").toLowerCase().includes(keyword)
      );
    });
  }, [permissionsQuery.data, search]);

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
    setSelectedPermissionIds((previous) => {
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

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={(next) => !next && onClose()} className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t("roles:assignPermissions.title")}</DialogTitle>
          <DialogDescription>
            {t("roles:assignPermissions.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4">
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
            <p className="text-sm text-muted-foreground">{t("common:loading")}</p>
          )}

          {roleDetailQuery.isError ? (
            <FormError message={getApiErrorMessage(roleDetailQuery.error, t("common:error"))} />
          ) : null}

          {permissionsQuery.isError ? (
            <FormError
              message={getApiErrorMessage(permissionsQuery.error, t("common:error"))}
            />
          ) : null}

          {filteredPermissions.length ? (
            <div className="max-h-80 space-y-2 overflow-y-auto rounded-lg border p-2">
              {filteredPermissions.map((permission) => (
                <label
                  key={permission.id}
                  className="flex items-start gap-2 rounded-md border p-2 text-sm"
                >
                  <Checkbox
                    checked={selectedPermissionIds.includes(permission.id)}
                    onChange={(event) =>
                      handleTogglePermission(permission.id, event.target.checked)
                    }
                    disabled={isSystemRole || saveMutation.isPending}
                  />
                  <span>
                    <span className="block font-medium">{permission.name}</span>
                    <span className="block text-xs text-muted-foreground">
                      {permission.code}
                    </span>
                    <span className="block text-xs text-muted-foreground">
                      {permission.description || "-"}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          ) : null}

          {permissionsQuery.isSuccess && !filteredPermissions.length ? (
            <p className="text-sm text-muted-foreground">
              {t("roles:assignPermissions.noPermissions")}
            </p>
          ) : null}
        </div>
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
