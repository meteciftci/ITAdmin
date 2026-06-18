namespace ITAdmin.Application.Common.Constants;

public static class AdDeletedObjectRestoreReadinessI18nKeys
{
    private const string Prefix = "deletedObjects.restore.readiness.";

    public static class Summary
    {
        public const string Ready = Prefix + "summary.ready";
        public const string Warning = Prefix + "summary.warning";
        public const string NotReady = Prefix + "summary.notReady";
        public const string SettingsIncomplete = Prefix + "summary.settingsIncomplete";
        public const string ModuleDisabled = Prefix + "summary.moduleDisabled";
        public const string UnexpectedFailure = Prefix + "summary.unexpectedFailure";
    }

    public static class Checks
    {
        public static class AdManagementSettings
        {
            public const string Title = Prefix + "checks.adManagementSettings.title";
            public const string SettingsIncompleteMessage = Prefix + "checks.adManagementSettings.settingsIncomplete.message";
            public const string SettingsIncompleteRemediation = Prefix + "checks.adManagementSettings.settingsIncomplete.remediation";
            public const string ModuleDisabledMessage = Prefix + "checks.adManagementSettings.moduleDisabled.message";
            public const string ModuleDisabledRemediation = Prefix + "checks.adManagementSettings.moduleDisabled.remediation";
            public const string ConnectionIncompleteMessage = Prefix + "checks.adManagementSettings.connectionIncomplete.message";
            public const string ConnectionIncompleteRemediation = Prefix + "checks.adManagementSettings.connectionIncomplete.remediation";
            public const string UnexpectedFailureMessage = Prefix + "checks.adManagementSettings.unexpectedFailure.message";
            public const string UnexpectedFailureRemediation = Prefix + "checks.adManagementSettings.unexpectedFailure.remediation";
        }

        public static class PowerShellTimeout
        {
            public const string Title = Prefix + "checks.powerShellTimeout.title";
            public const string Success = Prefix + "checks.powerShellTimeout.success";
            public const string Warning = Prefix + "checks.powerShellTimeout.warning";
            public const string Failed = Prefix + "checks.powerShellTimeout.failed";
            public const string RemediationInvalidRange = Prefix + "checks.powerShellTimeout.remediation.invalidRange";
            public const string RemediationRecommendHigher = Prefix + "checks.powerShellTimeout.remediation.recommendHigher";
        }

        public static class ActiveDirectoryPowerShellModule
        {
            public const string Title = Prefix + "checks.activeDirectoryPowerShellModule.title";
            public const string Success = Prefix + "checks.activeDirectoryPowerShellModule.success";
            public const string Failed = Prefix + "checks.activeDirectoryPowerShellModule.failed";
            public const string Remediation = Prefix + "checks.activeDirectoryPowerShellModule.remediation";
        }

        public static class RestoreAdObjectCommand
        {
            public const string Title = Prefix + "checks.restoreAdObjectCommand.title";
            public const string Success = Prefix + "checks.restoreAdObjectCommand.success";
            public const string Failed = Prefix + "checks.restoreAdObjectCommand.failed";
            public const string NotChecked = Prefix + "checks.restoreAdObjectCommand.notChecked";
            public const string Remediation = Prefix + "checks.restoreAdObjectCommand.remediation";
        }

        public static class AdwsPortConnectivity
        {
            public const string Title = Prefix + "checks.adwsPortConnectivity.title";
            public const string Success = Prefix + "checks.adwsPortConnectivity.success";
            public const string Failed = Prefix + "checks.adwsPortConnectivity.failed";
            public const string Remediation = Prefix + "checks.adwsPortConnectivity.remediation";
        }

        public static class RecycleBinFeature
        {
            public const string Title = Prefix + "checks.recycleBinFeature.title";
            public const string Success = Prefix + "checks.recycleBinFeature.success";
            public const string Disabled = Prefix + "checks.recycleBinFeature.disabled";
            public const string VerificationFailed = Prefix + "checks.recycleBinFeature.verificationFailed";
            public const string Remediation = Prefix + "checks.recycleBinFeature.remediation";
        }

        public static class ServiceAccountAdwsRead
        {
            public const string Title = Prefix + "checks.serviceAccountAdwsRead.title";
            public const string SuccessServiceAccount = Prefix + "checks.serviceAccountAdwsRead.success.serviceAccount";
            public const string SuccessProcessIdentity = Prefix + "checks.serviceAccountAdwsRead.success.processIdentity";
            public const string Failed = Prefix + "checks.serviceAccountAdwsRead.failed.default";
            public const string FailedAccessDenied = Prefix + "checks.serviceAccountAdwsRead.failed.accessDenied";
            public const string FailedTimeout = Prefix + "checks.serviceAccountAdwsRead.failed.timeout";
            public const string FailedConnection = Prefix + "checks.serviceAccountAdwsRead.failed.connection";
            public const string NotChecked = Prefix + "checks.serviceAccountAdwsRead.notChecked";
            public const string Remediation = Prefix + "checks.serviceAccountAdwsRead.remediation";
        }

        public static class RestorePermissionVerification
        {
            public const string Title = Prefix + "checks.restorePermissionVerification.title";
            public const string Verified = Prefix + "checks.restorePermissionVerification.verified";
            public const string NotVerified = Prefix + "checks.restorePermissionVerification.notVerified";
            public const string Remediation = Prefix + "checks.restorePermissionVerification.remediation";
        }
    }
}
