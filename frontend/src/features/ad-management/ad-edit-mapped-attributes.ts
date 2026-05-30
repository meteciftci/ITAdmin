import type {
  MappedAdUserAttribute,
  UpdateAdUserMappedAttributeRequest,
} from "@/features/ad-management/types";

function isMaskedPlaceholderValue(values: string[] | null | undefined): boolean {
  if (!values?.length) {
    return false;
  }

  return values.every((value) => value.trim() === "••••" || value.trim() === "");
}

function getBaselineValue(attribute: MappedAdUserAttribute): string {
  if (isMaskedPlaceholderValue(attribute.value)) {
    return "";
  }

  return attribute.value?.[0]?.trim() ?? "";
}

export function buildChangedMappedAttributes(
  editableMappedAttributes: MappedAdUserAttribute[],
  mappedValues: Record<string, string>,
): UpdateAdUserMappedAttributeRequest[] {
  const changed: UpdateAdUserMappedAttributeRequest[] = [];

  for (const attribute of editableMappedAttributes) {
    const newValue = mappedValues[attribute.logicalField]?.trim() ?? "";
    const baseline = getBaselineValue(attribute);

    if (attribute.isSensitive) {
      if (!newValue) {
        continue;
      }

      changed.push({
        logicalField: attribute.logicalField,
        value: newValue,
      });
      continue;
    }

    if (newValue === baseline) {
      continue;
    }

    changed.push({
      logicalField: attribute.logicalField,
      value: newValue ? newValue : null,
    });
  }

  return changed;
}
