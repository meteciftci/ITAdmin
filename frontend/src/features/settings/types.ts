export type SettingsOverview = {
  ldap: LdapSettings | null;
  applicationSettings: ApplicationSetting[];
  branding: BrandingSettings;
};

export type BrandingSettings = {
  applicationName: string;
  browserTitle: string;
  logoUrl: string | null;
};

export type BrandingLogoUploadResponse = {
  logoUrl: string;
};

export type LdapSettings = {
  id: string;
  name: string;
  host: string;
  port: number;
  useSsl: boolean;
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
  port: number;
  useSsl: boolean;
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
  port: number;
  useSsl: boolean;
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
