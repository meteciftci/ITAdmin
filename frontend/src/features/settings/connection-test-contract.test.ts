import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

/**
 * The Primary Authentication Directory and AD Management forms each expose two distinct test
 * actions: one that probes the unsaved form values, and one that re-probes the persisted
 * configuration using the stored secret. These are only meaningful if they really do reach
 * different endpoints with different payloads — a relabelled button posting the same body would
 * quietly tell an administrator that production is healthy when it is the form that was tested.
 */
describe("candidate vs saved connection test contract", () => {
  const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

  it("primary LDAP candidate test posts the form payload, saved test posts no body", () => {
    const source = read("./api.ts");

    assert.match(
      source,
      /validateLdapSettings\s*=[\s\S]{0,400}?apiClient\.post<ValidateLdapSettingsResponse>\(\s*"\/settings\/ldap\/validate",\s*payload/,
      "candidate test must post the form payload to /settings/ldap/validate",
    );

    const savedFn = source.slice(source.indexOf("validateSavedLdapSettings"));
    assert.match(savedFn, /"\/settings\/ldap\/validate-saved"/);
    // No second argument: the saved test must not resend anything from the form, secrets included.
    assert.doesNotMatch(
      savedFn.slice(0, savedFn.indexOf("return data")),
      /validate-saved",\s*[a-zA-Z{]/,
      "saved test must not send a request body",
    );
  });

  it("AD management candidate and saved tests use separate endpoints", () => {
    const source = read("../ad-management/api/settings-api.ts");

    const candidate = source.slice(source.indexOf("validateAdManagementCandidateSettings"));
    assert.match(candidate, /"\/ad-management\/settings\/validate-candidate",\s*payload/);

    const saved = source.slice(
      source.indexOf("export const validateAdManagementSettings"),
      source.indexOf("validateAdManagementCandidateSettings"),
    );
    assert.match(saved, /"\/ad-management\/settings\/validate"/);
    assert.doesNotMatch(saved, /validate-candidate/);
  });

  it("the two LDAP results are held in separate state and rendered in separate panels", () => {
    const hook = read("./hooks/useLdapSettingsSave.ts");

    // Distinct mutations and distinct result slots: one must never overwrite the other.
    assert.match(hook, /mutationFn:\s*validateLdapSettings/);
    assert.match(hook, /mutationFn:\s*validateSavedLdapSettings/);
    assert.match(hook, /candidateLdapValidation:\s*candidateResult/);
    assert.match(hook, /savedLdapValidation:\s*savedResult/);

    const form = read("./components/LdapSettingsForm.tsx");
    assert.match(form, /candidateValidation \?/);
    assert.match(form, /savedValidation \?/);
    // The candidate panel must not claim success once the form has drifted from what was tested.
    assert.match(form, /candidateValidation\.isValid && candidateValidationIsCurrent/);
  });
});
