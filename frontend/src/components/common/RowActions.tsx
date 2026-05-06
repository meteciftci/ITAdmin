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
          className="h-8 gap-1.5 rounded-md border border-primary/20 bg-primary/10 text-primary shadow-sm hover:bg-primary hover:text-primary-foreground dark:border-primary/30 dark:bg-primary/20 dark:text-primary-foreground dark:hover:bg-primary"
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
