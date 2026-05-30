const SAM_ACCOUNT_NAME_MAX_LENGTH = 20;
const FORBIDDEN_SAM_ACCOUNT_NAME_CHARS = /[/\\[\]:;|=,+?<>"]/;

export function buildDisplayNameFromParts(givenName: string, surname: string): string {
  return `${givenName.trim()} ${surname.trim()}`
    .trim()
    .replace(/\s+/g, " ");
}

export function isSamAccountNameValid(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed || trimmed.length > SAM_ACCOUNT_NAME_MAX_LENGTH) {
    return false;
  }

  if (FORBIDDEN_SAM_ACCOUNT_NAME_CHARS.test(trimmed)) {
    return false;
  }

  for (const character of trimmed) {
    const code = character.charCodeAt(0);
    if (code < 32) {
      return false;
    }
  }

  return true;
}

export function isUserPrincipalNameValid(value: string): boolean {
  const trimmed = value.trim();
  const atIndex = trimmed.indexOf("@");
  if (atIndex <= 0 || atIndex !== trimmed.lastIndexOf("@") || atIndex >= trimmed.length - 1) {
    return false;
  }

  const localPart = trimmed.slice(0, atIndex).trim();
  const domainPart = trimmed.slice(atIndex + 1).trim();
  if (!localPart || !domainPart) {
    return false;
  }

  return !localPart.includes("@") && !domainPart.includes("@");
}
