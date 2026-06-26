import assert from "node:assert/strict";
import { test } from "node:test";

import {
  validateAcquisitionForm,
  validateCompanyForm,
  validatePackageForm,
  validateProductForm,
} from "./form-validation.ts";

test("validateCompanyForm requires name", () => {
  assert.equal(validateCompanyForm("", "", "", "", ""), "nameRequired");
});

test("validateProductForm requires name", () => {
  assert.equal(validateProductForm(""), "nameRequired");
});

test("validateAcquisitionForm requires title", () => {
  assert.equal(validateAcquisitionForm(""), "titleRequired");
});

test("validatePackageForm enforces quantity minimum", () => {
  assert.equal(validatePackageForm("a", "b", 0), "quantityMin");
  assert.equal(validatePackageForm("", "b", 1), "acquisitionRequired");
  assert.equal(validatePackageForm("a", "", 1), "productRequired");
  assert.equal(validatePackageForm("a", "b", 5), null);
});
