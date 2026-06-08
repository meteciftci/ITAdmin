import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("ad-user-detail-button-styles", () => {
  it("exports outline and edit detail action classes with 32px sizing", () => {
    const stylesSource = readFileSync(
      new URL("./ad-user-detail-button-styles.ts", import.meta.url),
      "utf8",
    );

    assert.match(stylesSource, /export const adDetailOutlineButtonClass/);
    assert.match(stylesSource, /export const adDetailEditButtonClass/);
    assert.match(stylesSource, /export const adDetailActionButtonSizingClass/);
    assert.match(stylesSource, /h-8 min-h-8/);
    assert.match(stylesSource, /border-amber-500\/30/);
    assert.match(stylesSource, /adUserDetailManagerChangeButtonClass[\s\S]*adDetailActionButtonSizingClass/);
    assert.match(stylesSource, /adUserDetailManagerClearButtonClass[\s\S]*adDetailActionButtonSizingClass/);
  });
});

describe("AD user detail header action sizing", () => {
  it("uses shared 32px outline sizing for back and refresh actions", () => {
    const headerActionsSource = readFileSync(
      new URL("./components/ad-user-detail/AdUserDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(headerActionsSource, /adDetailOutlineButtonClass/);
    assert.match(headerActionsSource, /adDetailActionButtonSizingClass/);
    assert.match(headerActionsSource, /adDetailEditButtonClass/);
    assert.match(headerActionsSource, /buildAdUserDetailReturnState\(user\.id\)/);
    assert.doesNotMatch(headerActionsSource, /buttonVariants\(\{ variant: "outline", size: "sm" \}\)/);
  });
});

describe("AD group detail header action sizing", () => {
  it("uses shared 32px outline sizing for back and refresh actions", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /adDetailOutlineButtonClass/);
    assert.match(detailSource, /adDetailActionButtonSizingClass/);
    assert.match(detailSource, /adDetailEditButtonClass/);
    assert.match(detailSource, /buildAdGroupDetailReturnState\(group\.id\)/);
    assert.doesNotMatch(detailSource, /buttonVariants\(\{ variant: "outline", size: "sm" \}\)/);
  });
});
