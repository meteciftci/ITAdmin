import { useQuery } from "@tanstack/react-query";

import { getBrandingSettings } from "@/features/settings/api";
import type { BrandingSettings } from "@/features/settings/types";
import { getDefaultBrandingFooterText } from "@/lib/branding-footer";

export const BRANDING_QUERY_KEY = ["settings", "branding"] as const;

const DEFAULT_BRANDING: BrandingSettings = {
  applicationName: "ITAdmin",
  browserTitle: "ITAdmin",
  logoUrl: null,
  faviconUrl: "/favicon.svg",
  forgotPasswordUrl: null,
  footerText: getDefaultBrandingFooterText(),
};

export function useBrandingSettings() {
  const query = useQuery({
    queryKey: BRANDING_QUERY_KEY,
    queryFn: getBrandingSettings,
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
    placeholderData: DEFAULT_BRANDING,
  });

  return {
    ...query,
    data: query.data ?? DEFAULT_BRANDING,
  };
}
