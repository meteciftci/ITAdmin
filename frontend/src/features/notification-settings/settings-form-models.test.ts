import assert from "node:assert/strict";
import test from "node:test";

import {
  parseSmsStatusCodes,
  validateEmailProviderForm,
  validateSmsProviderForm,
} from "../notification-providers/provider-form-utils.ts";
import { renderTemplatePreview } from "./template-preview.ts";

test("provider validation rejects invalid connection values", () => {
  assert.deepEqual(
    validateEmailProviderForm({ host: "", port: "70000", fromAddress: "invalid", timeoutSeconds: "2" }),
    { host: "required", port: "range", fromAddress: "email", timeoutSeconds: "range" },
  );
  assert.deepEqual(
    validateSmsProviderForm({ endpointUrl: "ftp://example.test", timeoutSeconds: "30", successStatusCodes: "200,abc", authType: "ApiKeyHeader", apiKeyName: "" }),
    { endpointUrl: "url", successStatusCodes: "statusCodes", apiKeyName: "required" },
  );
});

test("SMS status codes and template preview preserve explicit semantics", () => {
  assert.deepEqual(parseSmsStatusCodes("200, 201,204"), [200, 201, 204]);
  assert.equal(
    renderTemplatePreview("Hello {{ displayName }} — {{unknown}}", new Map([["displayName", "Ada"]])),
    "Hello Ada — {{unknown}}",
  );
});
