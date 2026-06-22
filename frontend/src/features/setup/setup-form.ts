import type {
  CompleteSetupAdminUserRequest,
  CompleteSetupLdapRequest,
  CompleteSetupModulesRequest,
  CompleteSetupRequest,
} from "@/features/setup/api";

const DEFAULT_USER_SEARCH_FILTER = "(&(objectClass=user)(sAMAccountName={0}))";

const DIRECTORY_USER_NOT_FOUND = "Directory user could not be found.";
const DIRECTORY_USER_PROFILE_COULD_NOT_BE_LOADED_PREFIX = "Directory user profile could not be loaded";
const LDAP_OPERATION_TIMED_OUT_PREFIX = "LDAP operation timed out";

export type SetupLdapFormValues = {
  name: string;
  host: string;
  baseDn: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string;
  bindPassword: string;
};

export type SetupOuSelection = {
  distinguishedName: string;
  label: string;
};

export type SetupAdManagementFormValues = {
  isEnabled: boolean;
  usersSearchBase: SetupOuSelection | null;
  groupsSearchBase: SetupOuSelection | null;
  computersSearchBase: SetupOuSelection | null;
  disabledUsersOu: SetupOuSelection | null;
  defaultUserOu: SetupOuSelection | null;
  defaultGroupOu: SetupOuSelection | null;
  defaultComputerOu: SetupOuSelection | null;
  deletedObjectsEnabled: boolean;
};

export type SetupModulesFormValues = {
  adManagement: SetupAdManagementFormValues;
};

export type SetupAdminUserSelection = {
  userName: string;
  displayName: string;
  email?: string | null;
  distinguishedName?: string | null;
  directoryObjectId?: string | null;
};

export type SetupWizardFormValues = {
  setupKey: string;
  ldap: SetupLdapFormValues;
  modules: SetupModulesFormValues;
  adminUsers: SetupAdminUserSelection[];
};

export function createDefaultAdManagementFormValues(): SetupAdManagementFormValues {
  return {
    isEnabled: false,
    usersSearchBase: null,
    groupsSearchBase: null,
    computersSearchBase: null,
    disabledUsersOu: null,
    defaultUserOu: null,
    defaultGroupOu: null,
    defaultComputerOu: null,
    deletedObjectsEnabled: false,
  };
}

export function createDefaultSetupFormValues(defaultConnectionName: string): SetupWizardFormValues {
  return {
    setupKey: "",
    ldap: {
      name: defaultConnectionName,
      host: "",
      baseDn: "",
      userSearchFilter: DEFAULT_USER_SEARCH_FILTER,
      bindUserName: "",
      bindUserDomain: "",
      bindPassword: "",
    },
    modules: {
      adManagement: createDefaultAdManagementFormValues(),
    },
    adminUsers: [],
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function ouToPayload(selection: SetupOuSelection | null): string | null {
  if (!selection) {
    return null;
  }

  const dn = selection.distinguishedName.trim();
  return dn.length === 0 ? null : dn;
}

export function buildCompleteSetupLdapPayload(ldap: SetupLdapFormValues): CompleteSetupLdapRequest {
  return {
    name: ldap.name.trim() || "Default LDAP",
    host: ldap.host.trim(),
    baseDn: ldap.baseDn.trim(),
    userSearchFilter: ldap.userSearchFilter.trim(),
    bindUserName: ldap.bindUserName.trim(),
    bindUserDomain: emptyToNull(ldap.bindUserDomain),
    bindPassword: ldap.bindPassword,
  };
}

export function buildCompleteSetupModulesPayload(
  modules: SetupModulesFormValues,
): CompleteSetupModulesRequest {
  const adManagement = modules.adManagement;
  return {
    adManagement: {
      isEnabled: adManagement.isEnabled,
      usersSearchBase: adManagement.isEnabled ? ouToPayload(adManagement.usersSearchBase) : null,
      groupsSearchBase: adManagement.isEnabled ? ouToPayload(adManagement.groupsSearchBase) : null,
      computersSearchBase: adManagement.isEnabled ? ouToPayload(adManagement.computersSearchBase) : null,
      disabledUsersOu: adManagement.isEnabled ? ouToPayload(adManagement.disabledUsersOu) : null,
      defaultUserOu: adManagement.isEnabled ? ouToPayload(adManagement.defaultUserOu) : null,
      defaultGroupOu: adManagement.isEnabled ? ouToPayload(adManagement.defaultGroupOu) : null,
      defaultComputerOu: adManagement.isEnabled ? ouToPayload(adManagement.defaultComputerOu) : null,
      deletedObjectsEnabled: adManagement.deletedObjectsEnabled,
    },
  };
}

export function buildAdminUsersPayload(
  adminUsers: SetupAdminUserSelection[],
): CompleteSetupAdminUserRequest[] {
  return adminUsers.map((user) => ({
    userName: user.userName.trim(),
    distinguishedName: emptyToNull(user.distinguishedName ?? ""),
    directoryObjectId: emptyToNull(user.directoryObjectId ?? ""),
  }));
}

export function buildCompleteSetupRequest(values: SetupWizardFormValues): CompleteSetupRequest {
  return {
    setupKey: values.setupKey,
    ldap: buildCompleteSetupLdapPayload(values.ldap),
    modules: buildCompleteSetupModulesPayload(values.modules),
    adminUsers: buildAdminUsersPayload(values.adminUsers),
  };
}

export function isLdapFormComplete(ldap: SetupLdapFormValues): boolean {
  return (
    ldap.host.trim().length > 0 &&
    ldap.baseDn.trim().length > 0 &&
    ldap.userSearchFilter.trim().length > 0 &&
    ldap.bindUserName.trim().length > 0 &&
    ldap.bindPassword.trim().length > 0
  );
}

export function isAdManagementModuleValid(modules: SetupModulesFormValues): boolean {
  const adManagement = modules.adManagement;
  if (!adManagement.isEnabled) {
    return true;
  }

  return (
    adManagement.usersSearchBase !== null &&
    adManagement.groupsSearchBase !== null &&
    adManagement.computersSearchBase !== null
  );
}

export const MIN_OU_SEARCH_LENGTH = 2;
export const MIN_ADMIN_USER_SEARCH_LENGTH = 2;

export function isOuSearchBelowMinLength(search: string): boolean {
  const trimmed = search.trim();
  return trimmed.length > 0 && trimmed.length < MIN_OU_SEARCH_LENGTH;
}

export function shouldFetchOuSearchResults(open: boolean, search: string): boolean {
  if (!open) {
    return false;
  }

  const trimmed = search.trim();
  return trimmed.length === 0 || trimmed.length >= MIN_OU_SEARCH_LENGTH;
}

export function shouldFetchAdminUserSearchResults(ldapValidated: boolean, search: string): boolean {
  if (!ldapValidated) {
    return false;
  }

  return search.trim().length >= MIN_ADMIN_USER_SEARCH_LENGTH;
}

export function clearAdManagementOuSelections(
  modules: SetupModulesFormValues,
): SetupModulesFormValues {
  return {
    adManagement: {
      ...modules.adManagement,
      usersSearchBase: null,
      groupsSearchBase: null,
      computersSearchBase: null,
      disabledUsersOu: null,
      defaultUserOu: null,
      defaultGroupOu: null,
      defaultComputerOu: null,
    },
  };
}

export function applyLdapConfigChange(
  current: SetupWizardFormValues,
  ldap: SetupLdapFormValues,
): SetupWizardFormValues {
  return {
    ...current,
    ldap,
    modules: clearAdManagementOuSelections(current.modules),
    adminUsers: [],
  };
}

export function canAddAdminUser(
  adminUsers: SetupAdminUserSelection[],
  candidate: SetupAdminUserSelection,
): boolean {
  const normalizedUserName = candidate.userName.trim().toUpperCase();
  const normalizedObjectId = candidate.directoryObjectId?.trim();

  if (normalizedObjectId) {
    return !adminUsers.some(
      (user) => user.directoryObjectId?.trim().toLowerCase() === normalizedObjectId.toLowerCase(),
    );
  }

  return !adminUsers.some(
    (user) => user.userName.trim().toUpperCase() === normalizedUserName,
  );
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

export function summaryContainsSecrets(summaryText: string): boolean {
  const lowered = summaryText.toLowerCase();
  return (
    lowered.includes("setup key") ||
    lowered.includes("bind password") ||
    lowered.includes("jwt") ||
    lowered.includes("connection string")
  );
}
