import type { TFunction } from "i18next";

import type { AdDeletedObjectType } from "@/features/ad-management/types";

export function getAdDeletedObjectTypeLabel(t: TFunction, objectType: AdDeletedObjectType): string {
  switch (objectType) {
    case "User":
      return t("adManagement:deletedObjects.filters.typeUser");
    case "Group":
      return t("adManagement:deletedObjects.filters.typeGroup");
    case "Computer":
      return t("adManagement:deletedObjects.filters.typeComputer");
    default:
      return t("adManagement:deletedObjects.filters.typeUnknown");
  }
}
