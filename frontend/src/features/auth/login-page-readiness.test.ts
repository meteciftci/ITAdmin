import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const loginPageSource = readFileSync(
  new URL("./LoginPage.tsx", import.meta.url),
  "utf8",
);

describe("LoginPage readiness blocking", () => {
  it("shows BlockingStateCard while readiness is pending", () => {
    assert.match(loginPageSource, /readiness\.isPending/);
    assert.match(loginPageSource, /BlockingStateCard/);
    assert.match(loginPageSource, /auth:login\.serviceCheck\.title/);
    assert.match(loginPageSource, /auth:login\.serviceCheck\.description/);
  });

  it("does not render login form while readiness is pending or unhealthy", () => {
    const cardContentStart = loginPageSource.indexOf("<CardContent>");
    const cardContentEnd = loginPageSource.indexOf("</CardContent>", cardContentStart);
    const cardContent = loginPageSource.slice(cardContentStart, cardContentEnd);

    const pendingBranch = cardContent.slice(
      cardContent.indexOf("readiness.isPending"),
      cardContent.indexOf("readiness.data && !readiness.isHealthy"),
    );
    assert.doesNotMatch(pendingBranch, /id="userName"/);
    assert.doesNotMatch(pendingBranch, /id="password"/);
    assert.doesNotMatch(pendingBranch, /type="submit"/);

    const unhealthyBranch = cardContent.slice(
      cardContent.indexOf("readiness.data && !readiness.isHealthy"),
      cardContent.lastIndexOf(") : ("),
    );
    assert.match(unhealthyBranch, /ServiceUnavailableState/);
    assert.doesNotMatch(unhealthyBranch, /id="userName"/);
    assert.doesNotMatch(unhealthyBranch, /rememberMe/);
    assert.doesNotMatch(unhealthyBranch, /forgotPassword/);
  });

  it("renders login form only in healthy branch", () => {
    const healthyBranch = loginPageSource.slice(
      loginPageSource.lastIndexOf(") : ("),
    );
    assert.match(healthyBranch, /id="userName"/);
    assert.match(healthyBranch, /id="password"/);
    assert.match(healthyBranch, /type="submit"/);
    assert.match(healthyBranch, /routeNoticeMessage/);
  });

  it("keeps theme toggle and language switcher visible", () => {
    assert.match(loginPageSource, /ThemeToggle/);
    assert.match(loginPageSource, /PublicLanguageSwitcher/);
  });

  it("keeps retry action for unhealthy readiness", () => {
    assert.match(loginPageSource, /onRetry=\{\(\) =>/);
    assert.match(loginPageSource, /\["health", "readiness"\]/);
  });
});
