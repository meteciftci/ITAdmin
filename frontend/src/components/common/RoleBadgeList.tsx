import { Badge } from "@/components/ui/badge";

type RoleBadgeListProps = {
  roles: string[];
};

export function RoleBadgeList({ roles }: RoleBadgeListProps) {
  if (!roles.length) {
    return <span className="text-muted-foreground">-</span>;
  }

  return (
    <div className="flex flex-wrap gap-1">
      {roles.map((role) => (
        <Badge key={role} variant="secondary">
          {role}
        </Badge>
      ))}
    </div>
  );
}
