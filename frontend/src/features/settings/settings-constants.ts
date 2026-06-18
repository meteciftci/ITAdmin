import type { SessionSecuritySettings } from "@/features/settings/types";

export const SETTINGS_QUERY_KEY = ["settings", "overview"] as const;

export const BRANDING_APPLICATION_NAME_KEY = "Branding:ApplicationName";
export const BRANDING_BROWSER_TITLE_KEY = "Branding:BrowserTitle";
export const BRANDING_LOGO_URL_KEY = "Branding:LogoUrl";
export const BRANDING_FAVICON_URL_KEY = "Branding:FaviconUrl";
export const BRANDING_FORGOT_PASSWORD_URL_KEY = "Branding:ForgotPasswordUrl";
export const BRANDING_FOOTER_TEXT_KEY = "Branding:FooterText";
export const BRANDING_FOOTER_TEXT_MAX_LENGTH = 200;

export const SETTING_VALUE_TYPE_STRING = 1;
export const MAX_LOGO_BYTES = 2 * 1024 * 1024;
export const MAX_FAVICON_BYTES = 512 * 1024;

export type ApplicationSettingsTabValue = "ldap" | "branding" | "sessionSecurity";

export const DEFAULT_APPLICATION_SETTINGS_TAB: ApplicationSettingsTabValue = "branding";

export const DEFAULT_SESSION_SECURITY: SessionSecuritySettings = {
  accessTokenMinutes: 30,
  idleTimeoutMinutes: 30,
  idleWarningSeconds: 30,
  sessionRefreshTokenHours: 6,
  rememberMeRefreshTokenDays: 7,
  rememberMeEnabled: true,
};
