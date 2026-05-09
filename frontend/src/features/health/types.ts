export type ReadinessResponse = {
  status: "Healthy" | "Unhealthy" | string;
  apiAvailable: boolean;
  databaseAvailable: boolean;
  message: string;
  traceId?: string | null;
  checkedAt?: string;
};
