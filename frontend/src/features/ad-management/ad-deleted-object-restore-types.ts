export type AdDeletedObjectRestoreTargetMode = "OriginalLocation" | "TargetPath";

export type RestoreAdDeletedObjectRequest = {
  restoreTargetMode: AdDeletedObjectRestoreTargetMode;
  targetPathDistinguishedName?: string | null;
};
