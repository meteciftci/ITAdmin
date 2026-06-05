import type { TFunction } from "i18next";

import type { AdGroupScope } from "@/features/ad-management/types";

export function getAdGroupScopeLabel(t: TFunction<"adManagement">, scope: AdGroupScope | string): string {
  switch (scope) {
    case "Global":
      return t("groups.scope.global");
    case "DomainLocal":
      return t("groups.scope.domainLocal");
    case "Universal":
      return t("groups.scope.universal");
    default:
      return t("groups.scope.unknown");
  }
}

export function getAdGroupTypeLabel(
  t: TFunction<"adManagement">,
  securityEnabled: boolean,
): string {
  return securityEnabled ? t("groups.type.security") : t("groups.type.distribution");
}

export function getAdGroupMemberTypeLabel(
  t: TFunction<"adManagement">,
  type: string,
): string {
  switch (type) {
    case "User":
      return t("groups.memberTypes.user");
    case "Group":
      return t("groups.memberTypes.group");
    case "Computer":
      return t("groups.memberTypes.computer");
    default:
      return t("groups.memberTypes.unknown");
  }
}
