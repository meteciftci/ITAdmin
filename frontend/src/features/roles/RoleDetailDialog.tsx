import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import type { RoleDetail } from "@/features/roles/types";

type RoleDetailDialogProps = {
  role: RoleDetail | null;
  open: boolean;
  onClose: () => void;
};

const formatDateTime = (value: string | null): string => {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return date.toLocaleString();
};

export function RoleDetailDialog({ role, open, onClose }: RoleDetailDialogProps) {
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={(next) => !next && onClose()}>
        <DialogHeader>
          <DialogTitle>Role Details</DialogTitle>
          <DialogDescription>
            Review role information and assigned permissions.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4 text-sm">
          {!role ? (
            <p className="text-muted-foreground">Role details are unavailable.</p>
          ) : (
            <>
              {role.isSystem ? (
                <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-700">
                  System roles are managed by the application.
                </div>
              ) : null}
              <div className="grid gap-2 md:grid-cols-2">
                <p>
                  <span className="font-medium">Name:</span> {role.name}
                </p>
                <p>
                  <span className="font-medium">Code:</span> {role.code}
                </p>
                <p>
                  <span className="font-medium">Description:</span>{" "}
                  {role.description || "-"}
                </p>
                <p>
                  <span className="font-medium">Type:</span>{" "}
                  <Badge variant={role.isSystem ? "warning" : "secondary"}>
                    {role.isSystem ? "System" : "Custom"}
                  </Badge>
                </p>
                <p>
                  <span className="font-medium">Status:</span>{" "}
                  <Badge variant={role.isActive ? "success" : "outline"}>
                    {role.isActive ? "Active" : "Passive"}
                  </Badge>
                </p>
              </div>
              <Separator />
              <div className="space-y-2">
                <p className="font-medium">Permissions</p>
                {role.permissions.length ? (
                  <div className="max-h-48 overflow-y-auto rounded-lg border p-2">
                    <div className="flex flex-wrap gap-1">
                      {role.permissions.map((permission) => (
                        <Badge key={permission.id} variant="outline">
                          {permission.code}
                        </Badge>
                      ))}
                    </div>
                  </div>
                ) : (
                  <p className="text-muted-foreground">No permissions assigned.</p>
                )}
              </div>
              <Separator />
              <div className="grid gap-2 md:grid-cols-2">
                <p>
                  <span className="font-medium">Created At:</span>{" "}
                  {formatDateTime(role.createdAt)}
                </p>
                <p>
                  <span className="font-medium">Created By:</span>{" "}
                  {role.createdBy || "-"}
                </p>
                <p>
                  <span className="font-medium">Updated At:</span>{" "}
                  {formatDateTime(role.updatedAt)}
                </p>
                <p>
                  <span className="font-medium">Updated By:</span>{" "}
                  {role.updatedBy || "-"}
                </p>
              </div>
            </>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
