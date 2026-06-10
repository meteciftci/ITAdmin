import { buildAdComputerDetailPath } from "./ad-computer-detail-path.ts";
import { AD_COMPUTERS_LIST_PATH } from "./ad-computers-list-path.ts";

export const AD_COMPUTER_RETURN_STATE_KEY = "adComputerReturnPath";

export type AdComputerReturnState = {
  [AD_COMPUTER_RETURN_STATE_KEY]: string;
};

export function buildAdComputersListReturnState(): AdComputerReturnState {
  return {
    [AD_COMPUTER_RETURN_STATE_KEY]: AD_COMPUTERS_LIST_PATH,
  };
}

export function buildAdComputerDetailReturnState(computerId: string): AdComputerReturnState {
  return {
    [AD_COMPUTER_RETURN_STATE_KEY]: buildAdComputerDetailPath(computerId),
  };
}

export function resolveSafeAdComputerReturnPath(path: string | undefined | null): string {
  if (!path?.startsWith("/")) {
    return AD_COMPUTERS_LIST_PATH;
  }

  if (path.includes("..")) {
    return AD_COMPUTERS_LIST_PATH;
  }

  if (!path.startsWith(AD_COMPUTERS_LIST_PATH)) {
    return AD_COMPUTERS_LIST_PATH;
  }

  return path;
}

export function resolveAdComputerReturnPath(
  state: unknown,
  fallback = AD_COMPUTERS_LIST_PATH,
): string {
  if (!state || typeof state !== "object") {
    return fallback;
  }

  const returnPath = (state as AdComputerReturnState)[AD_COMPUTER_RETURN_STATE_KEY];
  return resolveSafeAdComputerReturnPath(returnPath) ?? fallback;
}
