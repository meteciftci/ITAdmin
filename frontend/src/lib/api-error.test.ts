import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from "axios";

import { getApiErrorInfo, getApiErrorMessage } from "./api-error.ts";

function createAxiosError(data: unknown, status = 400): AxiosError {
  const response = {
    status,
    data,
    statusText: "Bad Request",
    headers: {},
    config: {} as InternalAxiosRequestConfig,
  } satisfies AxiosResponse;

  return new AxiosError("Request failed", "ERR_BAD_REQUEST", {} as InternalAxiosRequestConfig, null, response);
}

describe("getApiErrorMessage", () => {
  it("returns PascalCase message when present", () => {
    const message = getApiErrorMessage(
      createAxiosError({ Message: "Validation failed." }),
      "fallback",
    );

    assert.equal(message, "Validation failed.");
  });

  it("prefers message over messageKey", () => {
    const message = getApiErrorMessage(
      createAxiosError({ message: "Direct message", messageKey: "errors:api.validation.description" }),
      "fallback",
    );

    assert.equal(message, "Direct message");
  });

  it("falls back to messageKey when message is missing", () => {
    const message = getApiErrorMessage(
      createAxiosError({ MessageKey: "adManagement:errors.notConfigured" }),
      "fallback",
    );

    assert.equal(message, "adManagement:errors.notConfigured");
  });

  it("returns fallback for unknown errors", () => {
    const message = getApiErrorMessage(new Error("boom"), "fallback");

    assert.equal(message, "fallback");
  });

  it("formats ASP.NET validation errors with field labels", () => {
    const message = getApiErrorMessage(
      createAxiosError({
        title: "One or more validation errors occurred.",
        status: 400,
        errors: {
          "$.defaultLicenseType": [
            "The JSON value could not be converted to ITAdmin.Domain.Enums.LicenseType.",
          ],
        },
      }),
      "fallback",
      {
        genericValidationMessage: "Please check the form fields.",
        fieldLabelResolver: (field) =>
          field === "defaultLicenseType" ? "Default license type" : null,
      },
    );

    assert.equal(message, "Default license type: Please check the form fields.");
    assert.doesNotMatch(message, /One or more validation errors occurred/);
  });

  it("does not return generic validation title alone when errors exist", () => {
    const message = getApiErrorMessage(
      createAxiosError({
        title: "One or more validation errors occurred.",
        errors: {
          name: ["Name is required."],
        },
      }),
      "fallback",
      {
        genericValidationMessage: "Please check the form fields.",
      },
    );

    assert.equal(message, "Name is required.");
  });
});

describe("getApiErrorInfo", () => {
  it("exposes response messageKey without replacing default descriptionKey", () => {
    const info = getApiErrorInfo(
      createAxiosError({ MessageKey: "adManagement:errors.notConfigured" }, 400),
    );

    assert.equal(info.responseMessageKey, "adManagement:errors.notConfigured");
    assert.equal(info.descriptionKey, "errors:api.validation.description");
    assert.equal(info.originalMessage, "adManagement:errors.notConfigured");
  });

  it("reads correlationId from PascalCase error payload", () => {
    const info = getApiErrorInfo(
      createAxiosError({ CorrelationId: "corr-123" }, 500),
    );

    assert.equal(info.traceId, "corr-123");
  });
});
