import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { getParentDistinguishedName } from "./ad-ldap-dn.ts";

describe("ad-ldap-dn", () => {
  it("returns parent OU for user distinguished name", () => {
    assert.equal(
      getParentDistinguishedName("CN=User,OU=Source,OU=Users,DC=example,DC=com"),
      "OU=Source,OU=Users,DC=example,DC=com",
    );
  });

  it("handles escaped comma in common name", () => {
    assert.equal(
      getParentDistinguishedName("CN=Ali\\, Veli,OU=Source,OU=Users,DC=example,DC=com"),
      "OU=Source,OU=Users,DC=example,DC=com",
    );
  });
});
