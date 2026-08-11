export type ProviderFieldErrors = Record<
  string,
  "required" | "range" | "email" | "url" | "statusCodes" | undefined
>;

export function parseSmsStatusCodes(value: string): number[] {
  return value.split(",").map((item) => Number(item.trim()));
}

export function validateEmailProviderForm(form: {
  host: string;
  port: string;
  fromAddress: string;
  timeoutSeconds: string;
}): ProviderFieldErrors {
  const errors: ProviderFieldErrors = {};
  const port = Number(form.port);
  const timeout = Number(form.timeoutSeconds);
  if (!form.host.trim()) errors.host = "required";
  if (!Number.isInteger(port) || port < 1 || port > 65535) errors.port = "range";
  if (!/^\S+@\S+\.\S+$/.test(form.fromAddress.trim())) errors.fromAddress = "email";
  if (!Number.isInteger(timeout) || timeout < 5 || timeout > 300) errors.timeoutSeconds = "range";
  return errors;
}

export function validateSmsProviderForm(form: {
  endpointUrl: string;
  timeoutSeconds: string;
  successStatusCodes: string;
  authType: string;
  apiKeyName: string;
}): ProviderFieldErrors {
  const errors: ProviderFieldErrors = {};
  const timeout = Number(form.timeoutSeconds);
  if (!/^https?:\/\//i.test(form.endpointUrl.trim())) errors.endpointUrl = "url";
  if (!Number.isInteger(timeout) || timeout < 5 || timeout > 300) errors.timeoutSeconds = "range";
  const codes = parseSmsStatusCodes(form.successStatusCodes);
  if (codes.length === 0 || codes.some((code) => !Number.isInteger(code) || code < 100 || code > 599)) {
    errors.successStatusCodes = "statusCodes";
  }
  if ((form.authType === "ApiKeyHeader" || form.authType === "ApiKeyQuery") && !form.apiKeyName.trim()) {
    errors.apiKeyName = "required";
  }
  return errors;
}
