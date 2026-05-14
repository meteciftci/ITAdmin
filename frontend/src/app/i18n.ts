import i18n from "i18next";
import { initReactI18next } from "react-i18next";

import trAuth from "@/locales/tr/auth.json";
import trAuditLogs from "@/locales/tr/auditLogs.json";
import trCommon from "@/locales/tr/common.json";
import trHome from "@/locales/tr/home.json";
import trErrors from "@/locales/tr/errors.json";
import trNavigation from "@/locales/tr/navigation.json";
import trPermissions from "@/locales/tr/permissions.json";
import trRoles from "@/locales/tr/roles.json";
import trSecurityLogs from "@/locales/tr/securityLogs.json";
import trSettings from "@/locales/tr/settings.json";
import trUsers from "@/locales/tr/users.json";

import enAuth from "@/locales/en/auth.json";
import enAuditLogs from "@/locales/en/auditLogs.json";
import enCommon from "@/locales/en/common.json";
import enHome from "@/locales/en/home.json";
import enErrors from "@/locales/en/errors.json";
import enNavigation from "@/locales/en/navigation.json";
import enPermissions from "@/locales/en/permissions.json";
import enRoles from "@/locales/en/roles.json";
import enSecurityLogs from "@/locales/en/securityLogs.json";
import enSettings from "@/locales/en/settings.json";
import enUsers from "@/locales/en/users.json";

export type SupportedLanguage = "tr" | "en";

export const normalizeLanguage = (value: string | null | undefined): SupportedLanguage => {
  if (value === "en") return "en";
  return "tr";
};

const resources = {
  tr: {
    common: trCommon.common,
    auth: trAuth.auth,
    auditLogs: trAuditLogs.auditLogs,
    navigation: trNavigation.navigation,
    home: trHome.home,
    errors: trErrors.errors,
    users: trUsers.users,
    roles: trRoles.roles,
    permissions: trPermissions.permissions,
    securityLogs: trSecurityLogs.securityLogs,
    settings: trSettings.settings,
  },
  en: {
    common: enCommon.common,
    auth: enAuth.auth,
    auditLogs: enAuditLogs.auditLogs,
    navigation: enNavigation.navigation,
    home: enHome.home,
    errors: enErrors.errors,
    users: enUsers.users,
    roles: enRoles.roles,
    permissions: enPermissions.permissions,
    securityLogs: enSecurityLogs.securityLogs,
    settings: enSettings.settings,
  },
} as const;

void i18n.use(initReactI18next).init({
  resources,
  lng: "tr",
  fallbackLng: "tr",
  interpolation: { escapeValue: false },
  ns: [
    "common",
    "auth",
    "navigation",
    "home",
    "errors",
    "users",
    "roles",
    "permissions",
    "auditLogs",
    "securityLogs",
    "settings",
  ],
  defaultNS: "common",
});

export { i18n };

