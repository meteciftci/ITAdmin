const RESERVED_CORE_AD_ATTRIBUTES = new Set(
  [
    "givenName",
    "sn",
    "displayName",
    "cn",
    "name",
    "sAMAccountName",
    "userPrincipalName",
    "mail",
    "department",
    "distinguishedName",
    "objectGUID",
    "memberOf",
    "userAccountControl",
    "lockoutTime",
    "pwdLastSet",
    "lastLogonTimestamp",
    "whenCreated",
    "whenChanged",
    "unicodePwd",
  ].map((name) => name.toLowerCase()),
);

export function isReservedCoreAdAttribute(attributeName: string): boolean {
  const normalized = attributeName.trim().toLowerCase();
  return normalized.length > 0 && RESERVED_CORE_AD_ATTRIBUTES.has(normalized);
}
