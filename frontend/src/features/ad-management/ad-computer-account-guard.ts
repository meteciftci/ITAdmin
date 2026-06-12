const DOMAIN_CONTROLLERS_PRIMARY_GROUP_ID = 516;
const SERVER_TRUST_ACCOUNT_FLAG = 0x2000;
const PARTIAL_SECRETS_ACCOUNT_FLAG = 0x04000000;

type ComputerAccountGuardSource = {
  primaryGroupId?: number | null;
  userAccountControl?: number | null;
};

export function isAdComputerAccountOperationRestricted(
  computer: ComputerAccountGuardSource,
): boolean {
  if (computer.primaryGroupId === DOMAIN_CONTROLLERS_PRIMARY_GROUP_ID) {
    return true;
  }

  if (computer.userAccountControl == null) {
    return false;
  }

  const flags = computer.userAccountControl;
  return (flags & SERVER_TRUST_ACCOUNT_FLAG) !== 0
    && (flags & PARTIAL_SECRETS_ACCOUNT_FLAG) !== 0;
}
