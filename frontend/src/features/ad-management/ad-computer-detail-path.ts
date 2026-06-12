import { AD_COMPUTERS_LIST_PATH } from "./ad-computers-list-path.ts";

export function buildAdComputerDetailPath(computerId: string): string {
  return `${AD_COMPUTERS_LIST_PATH}/${computerId}`;
}

export function buildAdComputerMoveOuPath(computerId: string): string {
  return `${AD_COMPUTERS_LIST_PATH}/${computerId}/move-ou`;
}
