import type { ReactNode } from "react";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

type SectionCardProps = {
  title?: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
};

export function SectionCard({ title, description, actions, children }: SectionCardProps) {
  return (
    <Card>
      {title || description || actions ? (
        <CardHeader className="space-y-3">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="space-y-1">
              {title ? <CardTitle>{title}</CardTitle> : null}
              {description ? (
                <p className="text-sm text-muted-foreground">{description}</p>
              ) : null}
            </div>
            {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
          </div>
        </CardHeader>
      ) : null}
      <CardContent>{children}</CardContent>
    </Card>
  );
}
