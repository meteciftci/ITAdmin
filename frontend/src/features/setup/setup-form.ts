import type { CompleteSetupLdapRequest, CompleteSetupRequest } from "@/features/setup/api";

const DEFAULT_USER_SEARCH_FILTER = "(&(objectClass=user)(sAMAccountName={0}))";

const DIRECTORY_USER_NOT_FOUND = "Directory user could not be found.";
const DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX = "Directory user profile could not be loaded";
const LDAP_OPERATION_TIMED_OUT_PREFIX = "LDAP operation timed out";

export type SetupLdapFormValues = {
  name: string;
  host: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string;
  bindPassword: string;
  nationalIdAttribute: string;
};

export type SetupAdminFormValues = {
  userName: string;
  password: string;
};

export type SetupFormValues = {
  setupKey: string;
  ldap: SetupLdapFormValues;
  admin: SetupAdminFormValues;
};

/**
 * Builds the initial setup form state. The default connection name is supplied
 * by the caller so it can be sourced from the i18n locale rather than hard-coded.
 */
export function createDefaultSetupFormValues(defaultConnectionName: string): SetupFormValues {
  return {
    setupKey: "",
    ldap: {
      name: defaultConnectionName,
      host: "",
      baseDn: "",
      userSearchBase: "",
      userSearchFilter: DEFAULT_USER_SEARCH_FILTER,
      bindUserName: "",
      bindUserDomain: "",
      bindPassword: "",
      nationalIdAttribute: "",
    },
    admin: {
      userName: "",
      password: "",
    },
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

export function buildCompleteSetupLdapPayload(
  ldap: SetupLdapFormValues,
): CompleteSetupLdapRequest {
  return {
    name: ldap.name.trim(),
    host: ldap.host.trim(),
    baseDn: ldap.baseDn.trim(),
    userSearchBase: ldap.userSearchBase.trim(),
    userSearchFilter: ldap.userSearchFilter.trim(),
    bindUserName: ldap.bindUserName.trim(),
    bindUserDomain: emptyToNull(ldap.bindUserDomain),
    bindPassword: ldap.bindPassword,
    nationalIdAttribute: emptyToNull(ldap.nationalIdAttribute),
  };
}

export function buildCompleteSetupRequest(values: SetupFormValues): CompleteSetupRequest {
  return {
    setupKey: values.setupKey,
    ldap: buildCompleteSetupLdapPayload(values.ldap),
    admin: {
      userName: values.admin.userName.trim(),
      password: values.admin.password,
    },
  };
}

export function resolveResponseMessage(message: string | undefined, fallback: string): string {
  const trimmed = message?.trim();
  return trimmed ? trimmed : fallback;
}

export type CompleteSetupFailureHints = {
  genericFallback: string;
  directoryUserNotFoundHint: string;
  directoryUserProfileHint: string;
  ldapTimeoutHint: string;
};

export function mapCompleteSetupFailureToast(
  backendMessage: string | undefined,
  hints: CompleteSetupFailureHints,
): string {
  const trimmed = backendMessage?.trim() ?? "";
  if (trimmed === DIRECTORY_USER_NOT_FOUND) {
    return hints.directoryUserNotFoundHint;
  }
  if (trimmed.startsWith(DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX)) {
    return hints.directoryUserProfileHint;
  }
  if (trimmed.startsWith(LDAP_OPERATION_TIMED_OUT_PREFIX)) {
    return hints.ldapTimeoutHint;
  }
  return trimmed.length > 0 ? trimmed : hints.genericFallback;
}
