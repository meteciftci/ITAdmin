export const AD_OPERATION_LOGS_PATH = "/monitoring/module-logs/ad-operation-logs";

export function buildAdOperationLogsPath(targetObjectGuid?: string | null): string {
  const trimmed = targetObjectGuid?.trim();
  if (!trimmed) {
    return AD_OPERATION_LOGS_PATH;
  }

  const params = new URLSearchParams({ targetObjectGuid: trimmed });
  return `${AD_OPERATION_LOGS_PATH}?${params.toString()}`;
}
