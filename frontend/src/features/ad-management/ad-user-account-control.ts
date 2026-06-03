export type AdUserAccountControlFlagKey =
  | "SCRIPT"
  | "ACCOUNTDISABLE"
  | "HOMEDIR_REQUIRED"
  | "LOCKOUT"
  | "PASSWD_NOTREQD"
  | "PASSWD_CANT_CHANGE"
  | "ENCRYPTED_TEXT_PWD_ALLOWED"
  | "TEMP_DUPLICATE_ACCOUNT"
  | "NORMAL_ACCOUNT"
  | "INTERDOMAIN_TRUST_ACCOUNT"
  | "WORKSTATION_TRUST_ACCOUNT"
  | "SERVER_TRUST_ACCOUNT"
  | "DONT_EXPIRE_PASSWORD"
  | "MNS_LOGON_ACCOUNT"
  | "SMARTCARD_REQUIRED"
  | "TRUSTED_FOR_DELEGATION"
  | "NOT_DELEGATED"
  | "USE_DES_KEY_ONLY"
  | "DONT_REQ_PREAUTH"
  | "PASSWORD_EXPIRED"
  | "TRUSTED_TO_AUTH_FOR_DELEGATION"
  | "PARTIAL_SECRETS_ACCOUNT";

export const AD_USER_ACCOUNT_CONTROL_FLAG_BITS: ReadonlyArray<{
  key: AdUserAccountControlFlagKey;
  mask: number;
}> = [
  { key: "SCRIPT", mask: 0x0001 },
  { key: "ACCOUNTDISABLE", mask: 0x0002 },
  { key: "HOMEDIR_REQUIRED", mask: 0x0008 },
  { key: "LOCKOUT", mask: 0x0010 },
  { key: "PASSWD_NOTREQD", mask: 0x0020 },
  { key: "PASSWD_CANT_CHANGE", mask: 0x0040 },
  { key: "ENCRYPTED_TEXT_PWD_ALLOWED", mask: 0x0080 },
  { key: "TEMP_DUPLICATE_ACCOUNT", mask: 0x0100 },
  { key: "NORMAL_ACCOUNT", mask: 0x0200 },
  { key: "INTERDOMAIN_TRUST_ACCOUNT", mask: 0x0800 },
  { key: "WORKSTATION_TRUST_ACCOUNT", mask: 0x1000 },
  { key: "SERVER_TRUST_ACCOUNT", mask: 0x2000 },
  { key: "DONT_EXPIRE_PASSWORD", mask: 0x10000 },
  { key: "MNS_LOGON_ACCOUNT", mask: 0x20000 },
  { key: "SMARTCARD_REQUIRED", mask: 0x40000 },
  { key: "TRUSTED_FOR_DELEGATION", mask: 0x80000 },
  { key: "NOT_DELEGATED", mask: 0x100000 },
  { key: "USE_DES_KEY_ONLY", mask: 0x200000 },
  { key: "DONT_REQ_PREAUTH", mask: 0x400000 },
  { key: "PASSWORD_EXPIRED", mask: 0x800000 },
  { key: "TRUSTED_TO_AUTH_FOR_DELEGATION", mask: 0x1000000 },
  { key: "PARTIAL_SECRETS_ACCOUNT", mask: 0x04000000 },
];

export type ParsedAdUserAccountControl = {
  rawValue: number | null;
  flags: AdUserAccountControlFlagKey[];
  unknownMask: number;
};

export function parseAdUserAccountControlFlags(
  value: number | null | undefined,
): ParsedAdUserAccountControl {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return { rawValue: null, flags: [], unknownMask: 0 };
  }

  const normalized = Math.trunc(value);
  const flags: AdUserAccountControlFlagKey[] = [];
  let knownMask = 0;

  for (const flag of AD_USER_ACCOUNT_CONTROL_FLAG_BITS) {
    if ((normalized & flag.mask) === flag.mask) {
      flags.push(flag.key);
      knownMask |= flag.mask;
    }
  }

  return {
    rawValue: normalized,
    flags,
    unknownMask: normalized & ~knownMask,
  };
}

export function formatAdUserAccountControlValue(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return "-";
  }

  return String(Math.trunc(value));
}
