import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

type LoadingStateProps = {
  text?: ReactNode;
};

export function LoadingState({ text }: LoadingStateProps) {
  const { t } = useTranslation(["common"]);
  return <p className="text-sm text-muted-foreground">{text ?? t("common:loading")}</p>;
}
