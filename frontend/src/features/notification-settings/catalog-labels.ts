import type { TFunction } from "i18next";

export function getCatalogModuleLabel(t: TFunction, moduleKey: string): string {
  const key = `notificationSettings:catalog.modules.${moduleKey}` as const;
  const translated = t(key);
  return translated === key ? moduleKey : translated;
}

export function getCatalogEventLabel(t: TFunction, moduleKey: string, eventKey: string): string {
  const key = `notificationSettings:catalog.events.${moduleKey}.${eventKey}` as const;
  const translated = t(key);
  return translated === key ? eventKey : translated;
}

export function getCatalogVariableLabel(t: TFunction, variableKey: string): string {
  const key = `notificationSettings:catalog.variables.${variableKey}.label` as const;
  const translated = t(key);
  return translated === key ? variableKey : translated;
}

export function getCatalogVariableDescription(t: TFunction, variableKey: string): string {
  const key = `notificationSettings:catalog.variables.${variableKey}.description` as const;
  const translated = t(key);
  return translated === key ? "" : translated;
}

export function getCatalogVariableExample(
  t: TFunction,
  variableKey: string,
  apiExample: string | null | undefined,
): string {
  if (apiExample) {
    return apiExample;
  }

  const key = `notificationSettings:catalog.variables.${variableKey}.example` as const;
  const translated = t(key);
  return translated === key ? "" : translated;
}

export function getChannelLabel(t: TFunction, channel: string): string {
  if (channel === "Sms") {
    return t("notificationSettings:channels.sms");
  }

  if (channel === "Email") {
    return t("notificationSettings:channels.email");
  }

  return channel;
}
