export type ParsedJsonLikeValue =
  | { kind: "empty" }
  | { kind: "pretty"; text: string }
  | { kind: "raw"; text: string };

const MAX_JSON_PARSE_DEPTH = 4;

function isEmptyString(value: string): boolean {
  return value.trim().length === 0;
}

function looksLikeJsonString(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) {
    return false;
  }

  const first = trimmed[0];
  return first === "{" || first === "[" || first === '"';
}

function isJsonContainer(value: unknown): value is Record<string, unknown> | unknown[] {
  return typeof value === "object" && value !== null;
}

function prettyPrintJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

/**
 * Repeatedly JSON.parse string values while the result is still a JSON-looking string.
 */
export function unwrapJsonLikeString(input: string, maxDepth = MAX_JSON_PARSE_DEPTH): unknown {
  let current: unknown = input;

  for (let depth = 0; depth < maxDepth; depth += 1) {
    if (typeof current !== "string") {
      return current;
    }

    const trimmed = current.trim();
    if (!trimmed || !looksLikeJsonString(trimmed)) {
      return current;
    }

    try {
      current = JSON.parse(trimmed) as unknown;
    } catch {
      return current;
    }
  }

  return current;
}

export function parseJsonLikeValue(value: unknown): ParsedJsonLikeValue {
  if (value === null || value === undefined) {
    return { kind: "empty" };
  }

  if (isJsonContainer(value)) {
    return { kind: "pretty", text: prettyPrintJson(value) };
  }

  if (typeof value !== "string") {
    return { kind: "raw", text: String(value) };
  }

  const trimmed = value.trim();
  if (isEmptyString(trimmed)) {
    return { kind: "empty" };
  }

  const unwrapped = unwrapJsonLikeString(trimmed);

  if (isJsonContainer(unwrapped)) {
    return { kind: "pretty", text: prettyPrintJson(unwrapped) };
  }

  if (typeof unwrapped === "string" && unwrapped !== trimmed) {
    return { kind: "raw", text: unwrapped };
  }

  if (unwrapped !== trimmed && !isJsonContainer(unwrapped)) {
    return { kind: "pretty", text: prettyPrintJson(unwrapped) };
  }

  return { kind: "raw", text: trimmed };
}

export function getJsonLikeDisplayText(value: unknown): string {
  const parsed = parseJsonLikeValue(value);
  if (parsed.kind === "empty") {
    return "";
  }
  return parsed.text;
}
