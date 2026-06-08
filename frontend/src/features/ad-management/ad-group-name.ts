const TURKISH_CHAR_MAP: Record<string, string> = {
  ç: "c",
  Ç: "c",
  ğ: "g",
  Ğ: "g",
  ı: "i",
  İ: "i",
  ö: "o",
  Ö: "o",
  ş: "s",
  Ş: "s",
  ü: "u",
  Ü: "u",
};

export const AD_GROUP_SAM_ACCOUNT_NAME_MAX_LENGTH = 64;

export function normalizeAdGroupSamAccountNameSuggestion(technicalName: string): string {
  const builder: string[] = [];

  for (const character of technicalName.trim()) {
    if (TURKISH_CHAR_MAP[character]) {
      builder.push(TURKISH_CHAR_MAP[character]);
      continue;
    }

    const lowered = character.toLocaleLowerCase("tr-TR");
    if (/[a-z0-9._-]/.test(lowered)) {
      builder.push(lowered);
    } else if (/\s/.test(character)) {
      builder.push(".");
    }
  }

  return collapseDots(builder.join(""))
    .replace(/^\.+|\.+$/g, "");
}

export function buildAdGroupSamAccountNameSuggestion(technicalName: string): string {
  return normalizeAdGroupSamAccountNameSuggestion(technicalName);
}

function collapseDots(value: string): string {
  return value.replace(/\.+/g, ".");
}
