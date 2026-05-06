import type { ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { DropdownMenuRoot } from "@/components/ui/dropdown-menu";

type RowActionsProps = {
  children: ReactNode;
  label?: ReactNode;
};

export function RowActions({ children, label }: RowActionsProps) {
  const { t } = useTranslation(["common"]);
  const text = label ?? t("common:actions.actions");
  return (
    <DropdownMenuRoot
      trigger={
        <Button
          variant="outline"
          size="sm"
          className="h-8 gap-1.5 rounded-md border-border bg-muted/60 text-foreground shadow-none hover:bg-accent hover:text-accent-foreground"
          title={typeof text === "string" ? text : t("common:actions.actions")}
        >
          {text}
          <ChevronDown className="size-3.5" />
        </Button>
      }
      contentProps={{
        align: "end",
        sideOffset: 6,
        collisionPadding: 12,
        avoidCollisions: true,
      }}
      content={<>{children}</>}
    />
  );
}
