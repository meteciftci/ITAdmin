import type { LicenseSeatPersonInput } from "@/features/license-management/types";

export type PersonDraft = {
  adObjectId: string | null;
  displayName: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  mail: string | null;
  department: string | null;
  title: string | null;
};

export const EMPTY_PERSON: PersonDraft = {
  adObjectId: null,
  displayName: "",
  samAccountName: null,
  userPrincipalName: null,
  mail: null,
  department: null,
  title: null,
};

/** Map the person form draft to the API payload. Assignments key off directory
 * identity, so no national id is sent from the UI. */
export function toPersonInput(draft: PersonDraft): LicenseSeatPersonInput {
  return {
    adObjectId: draft.adObjectId,
    displayName: draft.displayName.trim(),
    samAccountName: draft.samAccountName,
    userPrincipalName: draft.userPrincipalName,
    mail: draft.mail?.trim() || null,
    nationalId: null,
    department: draft.department,
    title: draft.title,
  };
}
