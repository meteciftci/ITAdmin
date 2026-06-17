import type { TFunction } from "i18next";
import { toast } from "sonner";

import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import type { AdManagementApiMessageFields } from "@/features/ad-management/types";

export type SequentialAddResult<T> = {
  item: T;
  success: boolean;
  source?: AdManagementApiMessageFields;
  error?: unknown;
};

export async function runSequentialMembershipAdd<T>(
  items: readonly T[],
  addOne: (item: T) => Promise<AdManagementApiMessageFields & { success: boolean }>,
): Promise<SequentialAddResult<T>[]> {
  const results: SequentialAddResult<T>[] = [];

  for (const item of items) {
    try {
      const response = await addOne(item);
      results.push({
        item,
        success: response.success,
        source: response,
      });
    } catch (error) {
      results.push({
        item,
        success: false,
        error,
      });
    }
  }

  return results;
}

export function partitionSequentialAddResults<T>(results: SequentialAddResult<T>[]) {
  const succeeded = results.filter((result) => result.success).map((result) => result.item);
  const failed = results.filter((result) => !result.success).map((result) => result.item);

  return { succeeded, failed, results };
}

type NotifySequentialAddOptions<T> = {
  t: TFunction;
  results: SequentialAddResult<T>[];
  allSuccessMessageKey: string;
  partialSuccessMessageKey: string;
  allFailedMessageKey: string;
  getDefaultErrorMessage: () => string;
};

export function notifySequentialAddResults<T>({
  t,
  results,
  allSuccessMessageKey,
  partialSuccessMessageKey,
  allFailedMessageKey,
}: NotifySequentialAddOptions<T>) {
  const successCount = results.filter((result) => result.success).length;
  const failedCount = results.length - successCount;

  if (successCount === results.length) {
    toast.success(t(allSuccessMessageKey));
    return;
  }

  if (successCount === 0) {
    const firstFailure = results.find((result) => !result.success);
    const message = firstFailure?.source
      ? resolveAdManagementApiMessage(t, firstFailure.source, allFailedMessageKey)
      : firstFailure?.error
        ? getAdManagementApiErrorMessage(firstFailure.error, t, allFailedMessageKey)
        : t(allFailedMessageKey);
    toast.error(message || t(allFailedMessageKey));
    return;
  }

  toast.warning(
    t(partialSuccessMessageKey, {
      successCount,
      failedCount,
    }),
  );
}
