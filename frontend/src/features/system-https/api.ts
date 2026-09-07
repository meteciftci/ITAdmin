import { apiClient } from "@/lib/api-client";
import type {
  ConfigureSystemHttpsInput,
  ConfigureSystemHttpsResponse,
  SystemHttpsStatus,
} from "@/features/system-https/types";

export const SYSTEM_HTTPS_STATUS_QUERY_KEY = ["system-https", "status"] as const;

export async function getSystemHttpsStatus(): Promise<SystemHttpsStatus> {
  const { data } = await apiClient.get<SystemHttpsStatus>("/system/https/status");
  return data;
}

export async function configureSystemHttps(
  input: ConfigureSystemHttpsInput,
): Promise<ConfigureSystemHttpsResponse> {
  const form = new FormData();
  form.append("pfx", input.pfx);
  form.append("password", input.password);
  form.append("httpsPort", String(input.httpsPort));
  form.append("redirectHttpToHttps", String(input.redirectHttpToHttps));

  const { data } = await apiClient.post<ConfigureSystemHttpsResponse>(
    "/system/https/configure",
    form,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return data;
}

export async function disableSystemHttps(): Promise<ConfigureSystemHttpsResponse> {
  const { data } = await apiClient.post<ConfigureSystemHttpsResponse>("/system/https/disable");
  return data;
}
