import type { AdUserGroupMembership, MappedAdUserAttribute } from "@/features/ad-management/types";

export const AD_USER_DETAIL_MAX_ATTRIBUTE_VALUE_LENGTH = 500;
export const AD_USER_GROUPS_SUMMARY_PREVIEW_COUNT = 10;

export function formatAdUserAttributeValues(values: string[]): string {
  const joined = values.join(", ");
  if (joined.length <= AD_USER_DETAIL_MAX_ATTRIBUTE_VALUE_LENGTH) {
    return joined;
  }

  return `${joined.slice(0, AD_USER_DETAIL_MAX_ATTRIBUTE_VALUE_LENGTH)}…`;
}

export function formatMappedAdUserAttributeValue(attribute: MappedAdUserAttribute): string {
  if (!attribute.value?.length) {
    return "-";
  }

  return formatAdUserAttributeValues(attribute.value);
}

export function hasMappedAttributeValue(attribute: MappedAdUserAttribute): boolean {
  return Boolean(attribute.value?.some((value) => value.trim().length > 0));
}

export function sortMappedAttributes(
  attributes: MappedAdUserAttribute[],
): MappedAdUserAttribute[] {
  return [...attributes].sort((left, right) => left.sortOrder - right.sortOrder);
}

export type MappedAttributeDisplayFilter = "filled" | "empty" | "all";

export function filterMappedAttributesForDisplay(
  attributes: MappedAdUserAttribute[],
  filter: MappedAttributeDisplayFilter,
): MappedAdUserAttribute[] {
  const sorted = sortMappedAttributes(attributes);

  switch (filter) {
    case "all":
      return sorted;
    case "empty":
      return sorted.filter((attribute) => !hasMappedAttributeValue(attribute));
    case "filled":
    default:
      return sorted.filter(hasMappedAttributeValue);
  }
}

export type AdUserGroupsSummary = {
  totalCount: number;
  previewGroups: AdUserGroupMembership[];
  remainingCount: number;
};

export function buildAdUserGroupsSummary(
  groups: AdUserGroupMembership[],
  previewCount = AD_USER_GROUPS_SUMMARY_PREVIEW_COUNT,
): AdUserGroupsSummary {
  const totalCount = groups.length;
  const previewGroups = groups.slice(0, previewCount);
  const remainingCount = Math.max(0, totalCount - previewGroups.length);

  return {
    totalCount,
    previewGroups,
    remainingCount,
  };
}

export function isGuidLike(value: string | undefined): boolean {
  if (!value?.trim()) {
    return false;
  }

  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}
