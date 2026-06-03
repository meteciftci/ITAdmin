import { useCallback } from "react";
import { useTranslation } from "react-i18next";

export function useAdOperationLogLabels() {
  const { t } = useTranslation("adOperationLogs");

  const getOperationLabel = useCallback(
    (value: string) => {
      const key = `operations.${value}` as const;
      const translated = t(key, { defaultValue: "" });
      return translated || value;
    },
    [t],
  );

  const getStatusLabel = useCallback(
    (status: string, changeStatus: string | null) => {
      if (changeStatus === "NoChangesDetected") {
        return t("statuses.noChangesDetected");
      }
      const key = `statuses.${status}` as const;
      const translated = t(key, { defaultValue: "" });
      return translated || status;
    },
    [t],
  );

  return { getOperationLabel, getStatusLabel };
}
