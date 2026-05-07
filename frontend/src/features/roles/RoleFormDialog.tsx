import { useMemo, useState } from "react";
import { useMutation } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { CheckboxField } from "@/components/common/CheckboxField";
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
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { createRole, updateRole } from "@/features/roles/api";
import type { RoleListItem } from "@/features/roles/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { useTranslation } from "react-i18next";

type RoleFormDialogProps = {
  open: boolean;
  mode: "create" | "edit";
  role?: RoleListItem | null;
  onClose: () => void;
  onSaved: () => void;
};

export function RoleFormDialog({
  open,
  mode,
  role,
  onClose,
  onSaved,
}: RoleFormDialogProps) {
  const { t } = useTranslation(["roles", "common"]);
  const isSystemRole = Boolean(role?.isSystem);
  const isEdit = mode === "edit";
  const dialogTitle = isEdit ? t("roles:form.editTitle") : t("roles:form.createTitle");
  const initialName = isEdit && role ? role.name : "";
  const initialCode = isEdit && role ? role.code : "";
  const initialDescription = isEdit && role ? (role.description ?? "") : "";
  const initialIsActive = isEdit && role ? role.isActive : true;
  const [name, setName] = useState(initialName);
  const [code, setCode] = useState(initialCode);
  const [description, setDescription] = useState(initialDescription);
  const [isActive, setIsActive] = useState(initialIsActive);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isSaveDisabled = useMemo(() => {
    if (isSystemRole) return true;
    if (!name.trim()) return true;
    if (!isEdit && !code.trim()) return true;
    return false;
  }, [code, isEdit, isSystemRole, name]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (isEdit && role) {
        return updateRole(role.id, {
          name: name.trim(),
          description: description.trim(),
          isActive,
        });
      }

      return createRole({
        name: name.trim(),
        code: code.trim(),
        description: description.trim(),
        isActive,
      });
    },
    onSuccess: () => {
      setErrorMessage(null);
      onSaved();
      onClose();
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(
          error,
          t("roles:form.error"),
        ),
      );
    },
  });

  const handleSave = () => {
    if (isSaveDisabled || saveMutation.isPending) return;
    saveMutation.mutate();
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setErrorMessage(null);
      onClose();
    }
  };

  return (
    <Dialog open={open}>
      <DialogContent
        key={`${open ? "open" : "closed"}-${mode}-${role?.id ?? "new"}`}
        onOpenChange={handleOpenChange}
      >
        <DialogHeader className="space-y-2">
          <DialogTitle>{dialogTitle}</DialogTitle>
          <DialogDescription>
            {t("roles:description")}
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

          <div className="space-y-2">
            <Label htmlFor="role-name">{t("roles:form.name")}</Label>
            <Input
              id="role-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={isSystemRole || saveMutation.isPending}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="role-code">{t("roles:form.code")}</Label>
            <Input
              id="role-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              readOnly={isEdit}
              disabled={isSystemRole || saveMutation.isPending || isEdit}
            />
            <p className="text-xs text-muted-foreground">
              {t("roles:form.codeHelp")}
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="role-description">{t("roles:form.description")}</Label>
            <Textarea
              id="role-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={isSystemRole || saveMutation.isPending}
            />
          </div>

          <CheckboxField
            id="role-isActive"
            label={t("common:status.active")}
            checked={isActive}
            onCheckedChange={setIsActive}
            disabled={isSystemRole || saveMutation.isPending}
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={handleSave}
            disabled={isSaveDisabled || saveMutation.isPending}
          >
            {saveMutation.isPending ? t("roles:assignPermissions.saving") : t("roles:form.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
