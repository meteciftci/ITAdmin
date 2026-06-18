import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const COMBOBOX_COMPONENTS = [
  "AdOuSearchCombobox.tsx",
  "AdGroupSearchCombobox.tsx",
  "AdGroupMultiSearchCombobox.tsx",
  "AdComputerGroupMultiSearchCombobox.tsx",
  "AdUserSearchCombobox.tsx",
] as const;

function readComboboxSource(filename: (typeof COMBOBOX_COMPONENTS)[number]): string {
  return readFileSync(
    new URL(`./components/${filename}`, import.meta.url),
    "utf8",
  );
}

describe("ad combobox popover standard", () => {
  it("defines shared trigger and popover style constants", () => {
    const stylesSource = readFileSync(
      new URL("./ad-combobox-styles.ts", import.meta.url),
      "utf8",
    );

    assert.match(stylesSource, /AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME/);
    assert.match(stylesSource, /AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME/);
    assert.match(stylesSource, /AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME/);
    assert.match(stylesSource, /AD_COMBOBOX_POPOVER_CONTENT_PROPS/);
    assert.match(stylesSource, /matchTriggerWidth: true/);
  });

  for (const filename of COMBOBOX_COMPONENTS) {
    it(`${filename} uses shared combobox width standard`, () => {
      const source = readComboboxSource(filename);

      assert.match(source, /AD_COMBOBOX_POPOVER_CONTENT_PROPS/);
      assert.match(source, /AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME/);
      assert.match(source, /AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME/);
      assert.match(source, /AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME/);
      assert.doesNotMatch(source, /min-w-\[/);
      assert.doesNotMatch(source, /sm:min-w-\[/);
      assert.doesNotMatch(source, /w-\[/);
    });
  }

  it("AdOuSearchCombobox truncates OU DN in list items", () => {
    const source = readComboboxSource("AdOuSearchCombobox.tsx");

    assert.match(source, /searchContext = "users"/);
    assert.match(source, /truncate font-mono text-xs text-muted-foreground/);
    assert.match(source, /min-w-0/);
  });
});

describe("ad combobox usage in OU flows", () => {
  it("organizational unit create page uses AdOuSearchCombobox", () => {
    const createPageSource = readFileSync(
      new URL("./AdOrganizationalUnitCreatePage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(createPageSource, /AdOuSearchCombobox/);
    assert.match(createPageSource, /searchContext="manage"/);
  });

  it("organizational unit move page uses AdOuSearchCombobox with manage context", () => {
    const movePageSource = readFileSync(
      new URL("./AdOrganizationalUnitMovePage.tsx", import.meta.url),
      "utf8",
    );
    const dialogsSource = readFileSync(
      new URL("./components/AdOrganizationalUnitDialogs.tsx", import.meta.url),
      "utf8",
    );

    assert.match(movePageSource, /AdOuSearchCombobox/);
    assert.match(movePageSource, /searchContext="manage"/);
    assert.match(movePageSource, /excludeDistinguishedName/);
    assert.doesNotMatch(dialogsSource, /AdMoveOrganizationalUnitDialog/);
    assert.doesNotMatch(dialogsSource, /AdOuSearchCombobox/);
  });

  it("user, group and computer OU move flows use AdOuSearchCombobox", () => {
    const userMoveSource = readFileSync(new URL("./AdMoveUserOuPage.tsx", import.meta.url), "utf8");
    const groupMoveSource = readFileSync(new URL("./AdMoveGroupOuPage.tsx", import.meta.url), "utf8");
    const computerMoveSource = readFileSync(
      new URL("./components/AdComputerMoveOuForm.tsx", import.meta.url),
      "utf8",
    );

    assert.match(userMoveSource, /AdOuSearchCombobox/);
    assert.match(groupMoveSource, /AdOuSearchCombobox/);
    assert.match(computerMoveSource, /AdOuSearchCombobox/);
    assert.doesNotMatch(userMoveSource, /searchContext="manage"/);
    assert.doesNotMatch(userMoveSource, /searchContext="groups"/);
    assert.doesNotMatch(userMoveSource, /searchContext="computers"/);
    assert.match(groupMoveSource, /searchContext="groups"/);
    assert.match(computerMoveSource, /searchContext="computers"/);
  });
});
