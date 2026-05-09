import { useQuery } from "@tanstack/react-query";

import { getReadinessStatus } from "@/features/health/api";
import type { ReadinessResponse } from "@/features/health/types";

type UseReadinessStatusOptions = {
  enabled?: boolean;
};

const isHealthyPayload = (data: ReadinessResponse | undefined): boolean =>
  Boolean(
    data &&
      data.status === "Healthy" &&
      data.apiAvailable &&
      data.databaseAvailable &&
      data.ldapAvailable,
  );

export function useReadinessStatus(options?: UseReadinessStatusOptions) {
  const query = useQuery({
    queryKey: ["health", "readiness"],
    queryFn: getReadinessStatus,
    retry: false,
    staleTime: 10_000,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    enabled: options?.enabled ?? true,
  });

  const data = query.data;
  const isHealthy = isHealthyPayload(data);
  const isApiUnavailable = Boolean(data && !data.apiAvailable);
  const isDatabaseUnavailable = Boolean(
    data && data.apiAvailable && !data.databaseAvailable,
  );
  const isLdapUnavailable = Boolean(
    data &&
      data.apiAvailable &&
      data.databaseAvailable &&
      !data.ldapAvailable,
  );

  return {
    ...query,
    isHealthy,
    isApiUnavailable,
    isDatabaseUnavailable,
    isLdapUnavailable,
  };
}
