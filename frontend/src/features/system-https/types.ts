export type SystemHttpsStatus = {
  agentAvailable: boolean;
  enabled: boolean;
  port: number;
  redirectHttpToHttps: boolean;
  certificateThumbprint: string | null;
  certificateSubject: string | null;
  certificateNotAfterUtc: string | null;
  message: string;
};

export type ConfigureSystemHttpsResponse = {
  enabled: boolean;
  port: number;
  redirectHttpToHttps: boolean;
  certificateThumbprint: string | null;
  certificateSubject: string | null;
  certificateNotAfterUtc: string | null;
  message: string;
};

export type ConfigureSystemHttpsInput = {
  pfx: File;
  password: string;
  httpsPort: number;
  redirectHttpToHttps: boolean;
};
