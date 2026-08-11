export function renderTemplatePreview(
  template: string,
  examples: ReadonlyMap<string, string>,
): string {
  return template.replace(
    /{{\s*([^{}]+?)\s*}}/g,
    (_, key: string) => examples.get(key) ?? `{{${key}}}`,
  );
}
