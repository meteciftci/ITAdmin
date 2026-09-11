const formatValue = (value: unknown): string => {
  if (Array.isArray(value)) return value.map(formatValue).join(", ");
  if (value && typeof value === "object") {
    return Object.entries(value)
      .map(([key, item]) => `${key}: ${formatValue(item)}`)
      .join(" · ");
  }
  if (value === null || value === undefined) return "-";
  return String(value);
};

export const formatDnsRecordValue = (value: string): string => {
  try {
    return formatValue(JSON.parse(value));
  } catch {
    return value || "-";
  }
};
