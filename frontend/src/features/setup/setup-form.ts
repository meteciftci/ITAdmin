import type { CompleteSetupLdapRequest, CompleteSetupRequest } from "@/features/setup/api";

export const STANDARD_LDAPS_PORT = "636";

const DEFAULT_LDAP_CONNECTION_NAME = "Default LDAP";
const DEFAULT_USER_SEARCH_FILTER = "(&(objectClass=user)(sAMAccountName={0}))";

/**
 * Backend message keys that the setup flow can surface to the user. They mirror
 * {@code SetupApiMessageKeys} on the backend and are resolved to localized text.
 */
export const SETUP_SECURE_CONNECTION_REQUIRED_MESSAGE_KEY = "apiMessages.setup.secureConnectionRequired";

const DIRECTORY_USER_NOT_FOUND = "Directory user could not be found.";
const DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX = "Directory user profile could not be loaded";
const LDAP_OPERATION_TIMED_OUT_PREFIX = "LDAP operation timed out";

export type SetupLdapFormValues = {
  name: string;
  host: string;
  port: string;
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

export const defaultSetupFormValues: SetupFormValues = {
  setupKey: "",
  ldap: {
    name: DEFAULT_LDAP_CONNECTION_NAME,
    host: "",
    port: STANDARD_LDAPS_PORT,
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

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

/**
 * Builds the LDAP portion of the complete-setup request. Connections are always
 * LDAPS, so {@code useSsl} is forced to {@code true} regardless of form state.
 */
export function buildCompleteSetupLdapPayload(
  ldap: SetupLdapFormValues,
  port: number,
): CompleteSetupLdapRequest {
  return {
    name: ldap.name.trim() || DEFAULT_LDAP_CONNECTION_NAME,
    host: ldap.host.trim(),
    port,
    useSsl: true,
    baseDn: ldap.baseDn.trim(),
    userSearchBase: ldap.userSearchBase.trim(),
    userSearchFilter: ldap.userSearchFilter.trim(),
    bindUserName: ldap.bindUserName.trim(),
    bindUserDomain: emptyToNull(ldap.bindUserDomain),
    bindPassword: ldap.bindPassword,
    nationalIdAttribute: emptyToNull(ldap.nationalIdAttribute),
  };
}

export function buildCompleteSetupRequest(
  values: SetupFormValues,
  port: number,
): CompleteSetupRequest {
  return {
    setupKey: values.setupKey,
    ldap: buildCompleteSetupLdapPayload(values.ldap, port),
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
  secureConnectionRequiredHint: string;
  directoryUserNotFoundHint: string;
  directoryUserProfileHint: string;
  ldapTimeoutHint: string;
};

export function mapCompleteSetupFailureToast(
  backendMessage: string | undefined,
  hints: CompleteSetupFailureHints,
): string {
  const trimmed = backendMessage?.trim() ?? "";
  if (trimmed === SETUP_SECURE_CONNECTION_REQUIRED_MESSAGE_KEY) {
    return hints.secureConnectionRequiredHint;
  }
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
