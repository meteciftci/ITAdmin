export type SessionSecuritySettings = {
  accessTokenMinutes: number;
  idleTimeoutMinutes: number;
  idleWarningSeconds: number;
  sessionRefreshTokenHours: number;
  rememberMeRefreshTokenDays: number;
  rememberMeEnabled: boolean;
};

export type UpdateSessionSecuritySettingsRequest = SessionSecuritySettings;

export type SettingsOverview = {
  ldap: LdapSettings | null;
  applicationSettings: ApplicationSetting[];
  branding: BrandingSettings;
  sessionSecurity: SessionSecuritySettings;
};

export type BrandingSettings = {
  applicationName: string;
  browserTitle: string;
  logoUrl: string | null;
  faviconUrl: string | null;
  forgotPasswordUrl: string | null;
  footerText: string;
};

export type BrandingLogoUploadResponse = {
  logoUrl: string;
};

export type BrandingFaviconUploadResponse = {
  faviconUrl: string;
};

export type LdapSettings = {
  id: string;
  name: string;
  host: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string | null;
  hasBindPassword: boolean;
  description: string | null;
  isActive: boolean;
};

export type ApplicationSetting = {
  key: string;
  value: string | null;
  valueType: number;
  description: string | null;
  isEncrypted: boolean;
  isSystem: boolean;
  isActive: boolean;
};

export type UpdateLdapSettingsRequest = {
  name: string;
  host: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string | null;
  bindPassword?: string;
  description: string | null;
};

export type ValidateLdapSettingsRequest = {
  name: string;
  host: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string | null;
  bindPassword?: string;
};

export type ValidateLdapSettingsResponse = {
  isValid: boolean;
  message: string;
};

export type UpdateApplicationSettingsRequestItem = {
  key: string;
  value: string | null;
  valueType: number;
};

export type UpdateApplicationSettingsRequest = {
  items: UpdateApplicationSettingsRequestItem[];
};
