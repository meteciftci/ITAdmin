import { useEffect, useMemo, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
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

type ApiErrorPayload = { message?: string };

const getErrorMessage = (error: unknown, fallback: string): string => {
  const apiError = error as AxiosError<ApiErrorPayload>;
  return apiError.response?.data?.message ?? fallback;
};

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
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [description, setDescription] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isSystemRole = Boolean(role?.isSystem);
  const isEdit = mode === "edit";
  const dialogTitle = isEdit ? "Edit Role" : "Add Role";

  useEffect(() => {
    if (!open) return;
    setErrorMessage(null);
    if (isEdit && role) {
      setName(role.name);
      setCode(role.code);
      setDescription(role.description ?? "");
      setIsActive(role.isActive);
      return;
    }

    setName("");
    setCode("");
    setDescription("");
    setIsActive(true);
  }, [open, isEdit, role]);

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
        getErrorMessage(
          error,
          isEdit ? "Role could not be updated." : "Role could not be created.",
        ),
      );
    },
  });

  const handleSave = () => {
    if (isSaveDisabled || saveMutation.isPending) return;
    saveMutation.mutate();
  };

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={(next) => !next && onClose()}>
        <DialogHeader>
          <DialogTitle>{dialogTitle}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? "Update role information."
              : "Create a custom role definition."}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4">
          {errorMessage ? (
            <Alert variant="destructive">
              <AlertTitle>Operation Failed</AlertTitle>
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
          ) : null}

          {isSystemRole ? (
            <Alert>
              <AlertTitle>System Role</AlertTitle>
              <AlertDescription>
                System roles are managed by the application and cannot be edited.
              </AlertDescription>
            </Alert>
          ) : null}

          <div className="space-y-2">
            <Label htmlFor="role-name">Name</Label>
            <Input
              id="role-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={isSystemRole || saveMutation.isPending}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="role-code">Code</Label>
            <Input
              id="role-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              readOnly={isEdit}
              disabled={isSystemRole || saveMutation.isPending || isEdit}
            />
            <p className="text-xs text-muted-foreground">
              Use letters, numbers, dot, dash or underscore. Spaces and Turkish
              characters are not allowed.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="role-description">Description</Label>
            <Textarea
              id="role-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={isSystemRole || saveMutation.isPending}
            />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={isActive}
              onChange={(event) => setIsActive(event.target.checked)}
              disabled={isSystemRole || saveMutation.isPending}
            />
            Active
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={isSaveDisabled || saveMutation.isPending}
          >
            {saveMutation.isPending ? "Saving..." : "Save"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
