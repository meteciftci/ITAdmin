import assert from "node:assert/strict";
import test from "node:test";

import { isBrandingFormDirty } from "./application-settings-model.ts";

const persisted = {
  applicationName: "ITAdmin",
  browserTitle: "ITAdmin Portal",
  logoUrl: null,
  faviconUrl: null,
  forgotPasswordUrl: null,
  footerText: "Operations",
};

test("branding dirty state includes text and selected assets", () => {
  const current = {
    applicationName: "ITAdmin",
    browserTitle: "ITAdmin Portal",
    forgotPasswordUrl: "",
    footerText: "Operations",
  };

  assert.equal(isBrandingFormDirty(current, persisted, false), false);
  assert.equal(isBrandingFormDirty({ ...current, browserTitle: "Admin" }, persisted, false), true);
  assert.equal(isBrandingFormDirty(current, persisted, true), true);
});

test("branding form is not dirty before settings hydrate", () => {
  assert.equal(
    isBrandingFormDirty(
      { applicationName: "ITAdmin", browserTitle: "ITAdmin", forgotPasswordUrl: "", footerText: "" },
      undefined,
      false,
    ),
    false,
  );
});
