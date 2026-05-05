import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import type { UserDetail } from "@/features/users/types";

type UserDetailDialogProps = {
  user: UserDetail;
};

const formatDateTime = (value: string | null): string => {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return date.toLocaleString();
};

export function UserDetailDialog({ user }: UserDetailDialogProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>User Details</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="grid gap-2 md:grid-cols-2">
          <p>
            <span className="font-medium">Display Name:</span> {user.displayName}
          </p>
          <p>
            <span className="font-medium">User Name:</span> {user.userName}
          </p>
          <p>
            <span className="font-medium">Email:</span> {user.email || "-"}
          </p>
          <p>
            <span className="font-medium">Status:</span>{" "}
            {user.isActive ? "Active" : "Passive"}
          </p>
          <p>
            <span className="font-medium">Directory Source:</span>{" "}
            {user.directorySource || "-"}
          </p>
          <p>
            <span className="font-medium">Directory Object ID:</span>{" "}
            {user.directoryObjectId || "-"}
          </p>
          <p>
            <span className="font-medium">National ID Masked:</span>{" "}
            {user.nationalIdMasked || "-"}
          </p>
          <p>
            <span className="font-medium">Last Login:</span>{" "}
            {formatDateTime(user.lastLoginAt)}
          </p>
        </div>
        <Separator />
        <p>
          <span className="font-medium">Roles:</span>{" "}
          {user.roles.length ? user.roles.join(", ") : "-"}
        </p>
        <Separator />
        <div className="grid gap-2 md:grid-cols-2">
          <p>
            <span className="font-medium">Created At:</span>{" "}
            {formatDateTime(user.createdAt)}
          </p>
          <p>
            <span className="font-medium">Created By:</span> {user.createdBy || "-"}
          </p>
          <p>
            <span className="font-medium">Updated At:</span>{" "}
            {formatDateTime(user.updatedAt)}
          </p>
          <p>
            <span className="font-medium">Updated By:</span> {user.updatedBy || "-"}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
