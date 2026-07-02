import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  isValidEmail,
  isValidUrl,
  validateCategoryForm,
  validateCompanyForm,
} from "./form-validation.ts";

describe("isValidEmail", () => {
  it("treats blank/whitespace as valid (optional field)", () => {
    assert.equal(isValidEmail(""), true);
    assert.equal(isValidEmail("   "), true);
  });

  it("accepts a well-formed address and trims surrounding space", () => {
    assert.equal(isValidEmail("user@example.com"), true);
    assert.equal(isValidEmail("  user@example.com  "), true);
  });

  it("rejects malformed addresses", () => {
    assert.equal(isValidEmail("user"), false);
    assert.equal(isValidEmail("user@"), false);
    assert.equal(isValidEmail("user@example"), false);
    assert.equal(isValidEmail("user @example.com"), false);
  });
});

describe("isValidUrl", () => {
  it("treats blank as valid (optional field)", () => {
    assert.equal(isValidUrl(""), true);
    assert.equal(isValidUrl("   "), true);
  });

  it("accepts http/https URLs", () => {
    assert.equal(isValidUrl("http://example.com"), true);
    assert.equal(isValidUrl("https://example.com/path"), true);
  });

  it("rejects URLs without an http(s) scheme", () => {
    assert.equal(isValidUrl("example.com"), false);
    assert.equal(isValidUrl("ftp://example.com"), false);
  });
});

describe("validateCategoryForm", () => {
  it("requires a name", () => {
    assert.equal(validateCategoryForm(""), "nameRequired");
    assert.equal(validateCategoryForm("   "), "nameRequired");
    assert.equal(validateCategoryForm("Graphics"), null);
  });
});

describe("validateCompanyForm", () => {
  it("requires a name before other checks", () => {
    assert.equal(validateCompanyForm("", "bad", "", ""), "nameRequired");
  });

  it("rejects an invalid primary or contact email", () => {
    assert.equal(validateCompanyForm("Acme", "not-an-email", "", ""), "invalidEmail");
    assert.equal(validateCompanyForm("Acme", "", "bad@", ""), "invalidEmail");
  });

  it("rejects an invalid website", () => {
    assert.equal(validateCompanyForm("Acme", "a@b.com", "", "example.com"), "invalidUrl");
  });

  it("passes when all fields are valid or blank", () => {
    assert.equal(validateCompanyForm("Acme", "", "", ""), null);
    assert.equal(
      validateCompanyForm("Acme", "a@b.com", "c@d.com", "https://acme.test"),
      null,
    );
  });
});
