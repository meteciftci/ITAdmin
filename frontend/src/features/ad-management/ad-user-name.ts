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

export function normalizeAdUsername(givenName: string, surname: string): string {
  const combined = `${givenName}.${surname}`;
  const builder: string[] = [];

  for (const character of combined.trim()) {
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
    .replace(/^\.+|\.+$/g, "")
    .slice(0, 20)
    .replace(/\.+$/g, "");
}

export function buildAdUserPrincipalName(username: string, defaultUpnSuffix: string): string {
  const suffix = defaultUpnSuffix.trim().replace(/^@+/, "").toLowerCase();
  return `${username}@${suffix}`;
}

function collapseDots(value: string): string {
  return value.replace(/\.+/g, ".");
}
