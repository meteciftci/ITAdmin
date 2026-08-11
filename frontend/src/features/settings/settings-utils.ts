import type { LdapFormValues } from "@/features/settings/components/LdapSettingsForm";
import type { SessionSecuritySettings } from "@/features/settings/types";

export function createEmptyLdapForm(): LdapFormValues {
  return {
    name: "",
    host: "",
    baseDn: "",
    userSearchBase: "",
    userSearchFilter: "",
    bindUserName: "",
    bindUserDomain: "",
    bindPassword: "",
    description: "",
    testUserName: "",
    testPassword: "",
  };
}

export function sessionSecurityFingerprint(s: SessionSecuritySettings): string {
  return [
    s.accessTokenMinutes,
    s.idleTimeoutMinutes,
    s.idleWarningSeconds,
    s.sessionRefreshTokenHours,
    s.rememberMeRefreshTokenDays,
    s.rememberMeEnabled ? "1" : "0",
  ].join("|");
}
