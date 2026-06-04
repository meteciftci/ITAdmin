import type {
  AdEffectiveGroupNestedItem,
  AdEffectiveGroupSummaryItem,
  AdMembershipPathNode,
  AdUserEffectiveGroupsResponse,
} from "@/features/ad-management/types";

type SearchableGroupFields = {
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description?: string | null;
  distinguishedName: string;
};

const TURKISH_SEARCH_FOLD_MAP: Readonly<Record<string, string>> = {
  "\u0131": "i",
  "\u011f": "g",
  "\u00fc": "u",
  "\u015f": "s",
  "\u00f6": "o",
  "\u00e7": "c",
};

export function normalizeEffectiveGroupSearchText(value: string): string {
  let normalized = value.trim().toLocaleLowerCase("tr-TR");

  for (const [from, to] of Object.entries(TURKISH_SEARCH_FOLD_MAP)) {
    normalized = normalized.replaceAll(from, to);
  }

  return normalized;
}

function collectSearchableValues(
  ...values: Array<string | null | undefined>
): string[] {
  return values
    .map((value) => value?.trim())
    .filter((value): value is string => Boolean(value));
}

function doesValueMatchQuery(value: string, normalizedQuery: string): boolean {
  return normalizeEffectiveGroupSearchText(value).includes(normalizedQuery);
}

function doValuesMatchQuery(
  values: Array<string | null | undefined>,
  normalizedQuery: string,
): boolean {
  return collectSearchableValues(...values).some((value) =>
    doesValueMatchQuery(value, normalizedQuery),
  );
}

function doesPathMatchSearch(path: AdMembershipPathNode[], normalizedQuery: string): boolean {
  return path.some((node) =>
    doValuesMatchQuery(
      [node.displayName, node.name, node.samAccountName, node.distinguishedName],
      normalizedQuery,
    ),
  );
}

export function doesDirectGroupMatchSearch(
  group: SearchableGroupFields,
  query: string,
): boolean {
  const normalizedQuery = normalizeEffectiveGroupSearchText(query);
  if (!normalizedQuery) {
    return true;
  }

  return doValuesMatchQuery(
    [
      group.displayName,
      group.name,
      group.samAccountName,
      group.description,
      group.distinguishedName,
    ],
    normalizedQuery,
  );
}

export function doesEffectiveGroupMatchSearch(
  group: AdEffectiveGroupNestedItem,
  query: string,
): boolean {
  const normalizedQuery = normalizeEffectiveGroupSearchText(query);
  if (!normalizedQuery) {
    return true;
  }

  if (
    doValuesMatchQuery(
      [
        group.displayName,
        group.name,
        group.samAccountName,
        group.description,
        group.distinguishedName,
      ],
      normalizedQuery,
    )
  ) {
    return true;
  }

  return doesPathMatchSearch(group.path, normalizedQuery);
}

export function filterDirectGroupsBySearch(
  groups: AdEffectiveGroupSummaryItem[],
  query: string,
): AdEffectiveGroupSummaryItem[] {
  const normalizedQuery = normalizeEffectiveGroupSearchText(query);
  if (!normalizedQuery) {
    return groups;
  }

  return groups.filter((group) => doesDirectGroupMatchSearch(group, normalizedQuery));
}

export function filterEffectiveGroupsBySearch(
  groups: AdEffectiveGroupNestedItem[],
  query: string,
): AdEffectiveGroupNestedItem[] {
  const normalizedQuery = normalizeEffectiveGroupSearchText(query);
  if (!normalizedQuery) {
    return groups;
  }

  return groups.filter((group) => doesEffectiveGroupMatchSearch(group, normalizedQuery));
}

export function filterEffectiveGroupMemberships(
  data: Pick<AdUserEffectiveGroupsResponse, "directGroups" | "effectiveGroups">,
  query: string,
): Pick<AdUserEffectiveGroupsResponse, "directGroups" | "effectiveGroups"> {
  return {
    directGroups: filterDirectGroupsBySearch(data.directGroups, query),
    effectiveGroups: filterEffectiveGroupsBySearch(data.effectiveGroups, query),
  };
}
