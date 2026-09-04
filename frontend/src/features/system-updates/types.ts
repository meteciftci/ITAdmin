export type SystemUpdateOperation = {
  operationId: string | null;
  phase: string;
  targetCommit: string | null;
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
  activeCommit: string | null;
  previousCommit: string | null;
  branch: string;
  builtAtUtc: string | null;
  healthy: boolean;
  updateAvailable: boolean;
  commitsBehind: number;
  latestCommit: string | null;
  latestSubject: string | null;
  operation: SystemUpdateOperation | null;
  checkedAtUtc: string;
};

export type InstallSystemUpdateResponse = {
  operationId: string | null;
  targetCommit: string | null;
  message: string;
};
