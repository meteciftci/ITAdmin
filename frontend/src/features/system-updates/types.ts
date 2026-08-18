export type SystemUpdateOperation = {
  operationId: string | null;
  phase: string;
  targetVersion: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  message: string;
};

export type SystemUpdateStatus = {
  agentAvailable: boolean;
  repositoryAccessible: boolean;
  repositoryStatus: string;
  message: string;
  installationPhase: string | null;
  activeVersion: string | null;
  previousVersion: string | null;
  healthy: boolean;
  latestVersion: string | null;
  latestSourceCommit: string | null;
  latestPublishedAtUtc: string | null;
  latestDescription: string | null;
  updateAvailable: boolean;
  operation: SystemUpdateOperation | null;
  checkedAtUtc: string;
};

export type InstallSystemUpdateResponse = {
  operationId: string;
  targetVersion: string;
  message: string;
};
