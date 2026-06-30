import { resolveSafeReturnPath } from "@/features/ad-management/ad-return-path.ts";

export type LicenseRequestNavigationState = {
  returnTo?: string;
};

export function readLicenseRequestReturnToFromState(state: unknown): string | undefined {
  if (!state || typeof state !== "object") {
    return undefined;
  }

  const returnTo = (state as LicenseRequestNavigationState).returnTo;
  return typeof returnTo === "string" ? returnTo : undefined;
}

export function resolveLicenseRequestReturnPath(
  state: unknown,
  fallback: string,
): string {
  const returnTo = readLicenseRequestReturnToFromState(state);
  if (!returnTo?.trim()) {
    return fallback;
  }

  return resolveSafeReturnPath(returnTo);
}

export function buildLicenseRequestReturnState(returnTo: string): LicenseRequestNavigationState {
  return { returnTo };
}
