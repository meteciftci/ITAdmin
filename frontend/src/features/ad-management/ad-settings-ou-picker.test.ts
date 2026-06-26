import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it } from "node:test";

const currentDir = dirname(fileURLToPath(import.meta.url));

function readSource(relativePath: string): string {
  return readFileSync(join(currentDir, relativePath), "utf8");
}

describe("AdSettingsOuPickerField", () => {
  it("uses settings organizational units endpoint", () => {
    const pickerSource = readSource("components/AdSettingsOuPickerField.tsx");
    const apiSource = readSource("api.ts");

    assert.match(pickerSource, /getAdManagementSettingsOrganizationalUnits/);
    assert.match(apiSource, /\/ad-management\/settings\/organizational-units/);
    assert.doesNotMatch(pickerSource, /searchOrganizationalUnits/);
    assert.doesNotMatch(pickerSource, /searchGroupOrganizationalUnits/);
    assert.doesNotMatch(pickerSource, /searchComputerOrganizationalUnits/);
  });

  it("shows settings-specific load failed message", () => {
    const source = readSource("components/AdSettingsOuPickerField.tsx");
    assert.match(source, /settings:adManagement\.ouPicker\.loadFailed/);
  });

  it("uses compact card layout with right-aligned actions", () => {
    const source = readSource("components/AdSettingsOuPickerField.tsx");
    assert.match(source, /sm:flex-row sm:items-start sm:justify-between/);
    assert.match(source, /flex shrink-0 flex-wrap gap-2 sm:justify-end/);
    assert.match(source, /truncate font-mono text-xs text-muted-foreground/);
  });
});

describe("settings OU forms", () => {
  it("scopes form uses AdSettingsOuPickerField and responsive grid", () => {
    const source = readSource("components/AdManagementScopesForm.tsx");
    assert.match(source, /AdSettingsOuPickerField/);
    assert.match(source, /xl:grid-cols-2/);
    assert.doesNotMatch(source, /AdOuPickerField/);
    assert.doesNotMatch(source, /searchContext/);
  });

  it("creation defaults form uses AdSettingsOuPickerField and responsive grid", () => {
    const source = readSource("components/AdCreationDefaultsForm.tsx");
    assert.match(source, /AdSettingsOuPickerField/);
    assert.match(source, /xl:grid-cols-3/);
    assert.doesNotMatch(source, /AdOuPickerField/);
    assert.doesNotMatch(source, /searchContext/);
  });

  it("scopes form preserves merge payload fields", () => {
    const source = readSource("components/AdManagementScopesForm.tsx");
    assert.match(source, /usersRootOu/);
    assert.match(source, /disabledUsersOu/);
    assert.match(source, /groupsSearchBase/);
    assert.match(source, /computersSearchBase/);
    assert.match(source, /buildUpdateAdManagementSettingsPayload/);
  });

  it("creation defaults form preserves default OU payload fields", () => {
    const source = readSource("components/AdCreationDefaultsForm.tsx");
    assert.match(source, /defaultUserOu/);
    assert.match(source, /defaultGroupOu/);
    assert.match(source, /defaultComputerOu/);
    assert.match(source, /buildUpdateAdManagementSettingsPayload/);
  });

  it("settings picker supports clear to null", () => {
    const source = readSource("components/AdSettingsOuPickerField.tsx");
    assert.match(source, /onChange\(null\)/);
    assert.match(source, /allowClear/);
  });

  it("scopes form passes explicit null OU values into payload builder", () => {
    const scopesSource = readSource("components/AdManagementScopesForm.tsx");
    const payloadSource = readSource("ad-management-settings-payload.ts");

    assert.match(scopesSource, /usersRootOu/);
    assert.match(scopesSource, /disabledUsersOu/);
    assert.match(payloadSource, /resolveNullableOverride/);
    assert.match(payloadSource, /usersRootOu: resolveNullableOverride/);
  });
});

describe("AdOuSearchCombobox operational usage", () => {
  it("remains a popover combobox for create and move screens", () => {
    const source = readSource("components/AdOuSearchCombobox.tsx");
    assert.match(source, /Popover/);
    assert.match(source, /AD_COMBOBOX_POPOVER_CONTENT_PROPS/);
    assert.doesNotMatch(source, /AdSettingsOuPickerField/);
    assert.doesNotMatch(source, /getAdManagementSettingsOrganizationalUnits/);
  });

  it("create user page still imports AdOuSearchCombobox", () => {
    const source = readSource("AdCreateUserPage.tsx");
    assert.match(source, /AdOuSearchCombobox/);
    assert.doesNotMatch(source, /AdSettingsOuPickerField/);
  });
});
