namespace SasPortal.Application.Common.Constants;

public static class AdManagementApiMessageKeys
{
    private const string Prefix = "apiMessages.";

    public static class Common
    {
        public const string NotConfigured = Prefix + "common.notConfigured";
        public const string ModuleDisabled = Prefix + "common.moduleDisabled";
        public const string MissingServiceAccountPassword = Prefix + "common.missingServiceAccountPassword";
        public const string ConnectionFailed = Prefix + "common.connectionFailed";
        public const string InvalidRequest = Prefix + "common.invalidRequest";
        public const string LdapsRequired = Prefix + "common.ldapsRequired";
    }

    public static class SettingsValidation
    {
        public const string MissingRequiredSettings = Prefix + "settingsValidation.missingRequiredSettings";
        public const string ServiceAccountBindFailed = Prefix + "settingsValidation.serviceAccountBindFailed";
        public const string DomainFqdnUnreachable = Prefix + "settingsValidation.domainFqdnUnreachable";
        public const string BaseDnNotResolved = Prefix + "settingsValidation.baseDnNotResolved";
        public const string DefaultNamingContextNotResolved = Prefix + "settingsValidation.defaultNamingContextNotResolved";
        public const string UsersRootOuNotResolved = Prefix + "settingsValidation.usersRootOuNotResolved";
        public const string DisabledUsersOuNotResolved = Prefix + "settingsValidation.disabledUsersOuNotResolved";
        public const string GroupsSearchBaseNotResolved = Prefix + "settingsValidation.groupsSearchBaseNotResolved";
        public const string ComputersSearchBaseNotResolved = Prefix + "settingsValidation.computersSearchBaseNotResolved";
        public const string PreferredDcUnreachable = Prefix + "settingsValidation.preferredDcUnreachable";
        public const string ValidationSucceeded = Prefix + "settingsValidation.validationSucceeded";
    }

    public static class Settings
    {
        public const string UpdateSucceeded = Prefix + "settings.updateSucceeded";
        public const string ServiceAccountPasswordRequired = Prefix + "settings.serviceAccountPasswordRequired";
        public const string MissingRequiredFields = Prefix + "settings.missingRequiredFields";
        public const string LdapPortOutOfRange = Prefix + "settings.ldapPortOutOfRange";
        public const string PowerShellTimeoutOutOfRange = Prefix + "settings.powerShellTimeoutOutOfRange";
        public const string DefaultUpnSuffixInvalid = Prefix + "settings.defaultUpnSuffixInvalid";
    }

    public static class NotificationSettings
    {
        public const string InvalidEvent = Prefix + "notificationSettings.invalidEvent";
        public const string InvalidChannel = Prefix + "notificationSettings.invalidChannel";
        public const string DuplicateRule = Prefix + "notificationSettings.duplicateRule";
        public const string RecipientSourceRequired = Prefix + "notificationSettings.recipientSourceRequired";
        public const string InvalidSmsRecipientSource = Prefix + "notificationSettings.invalidSmsRecipientSource";
        public const string InvalidEmailRecipientSource = Prefix + "notificationSettings.invalidEmailRecipientSource";
        public const string RecipientSourceValueRequired = Prefix + "notificationSettings.recipientSourceValueRequired";
    }

    public static class MappedAttributes
    {
        public const string InvalidLogicalField = Prefix + "mappedAttributes.invalidLogicalField";
        public const string NotEditable = Prefix + "mappedAttributes.notEditable";
        public const string NotFound = Prefix + "mappedAttributes.notFound";
        public const string ReservedAttribute = Prefix + "mappedAttributes.reservedAttribute";
        public const string InvalidAttributeName = Prefix + "mappedAttributes.invalidAttributeName";
        public const string InvalidPhoneFormat = Prefix + "mappedAttributes.invalidPhoneFormat";
        public const string InvalidEmailFormat = Prefix + "mappedAttributes.invalidEmailFormat";
        public const string InvalidNumberFormat = Prefix + "mappedAttributes.invalidNumberFormat";
    }

    public static class AttributeMappings
    {
        public const string LogicalFieldInvalid = Prefix + "attributeMappings.logicalFieldInvalid";
        public const string DisplayNameRequired = Prefix + "attributeMappings.displayNameRequired";
        public const string DisplayNameTooLong = Prefix + "attributeMappings.displayNameTooLong";
        public const string AttributeNameInvalid = Prefix + "attributeMappings.attributeNameInvalid";
        public const string ValidationTypeInvalid = Prefix + "attributeMappings.validationTypeInvalid";
        public const string DuplicateLogicalField = Prefix + "attributeMappings.duplicateLogicalField";
        public const string NotFound = Prefix + "attributeMappings.notFound";
        public const string CreateSuccess = Prefix + "attributeMappings.createSuccess";
        public const string UpdateSuccess = Prefix + "attributeMappings.updateSuccess";
        public const string DeleteSuccess = Prefix + "attributeMappings.deleteSuccess";
    }

    public static class Users
    {
        public const string NotFound = Prefix + "users.notFound";
        public const string QueryFailed = Prefix + "users.queryFailed";
        public const string CreateSuccess = Prefix + "users.createSuccess";
        public const string CreateFailed = Prefix + "users.createFailed";
        public const string UpdateFailed = Prefix + "users.updateFailed";
        public const string OuMoveSuccess = Prefix + "users.ouMoveSuccess";
        public const string OuMoveFailed = Prefix + "users.ouMoveFailed";
        public const string TargetOuRequired = Prefix + "users.targetOuRequired";
        public const string AlreadyInTargetOu = Prefix + "users.alreadyInTargetOu";
        public const string InvalidTargetOu = Prefix + "users.invalidTargetOu";
        public const string NamingConflictFailed = Prefix + "users.namingConflictFailed";
        public const string MissingUpnSuffix = Prefix + "users.missingUpnSuffix";
        public const string InvalidUpnSuffix = Prefix + "users.invalidUpnSuffix";
        public const string ManagerUpdateFailed = Prefix + "users.managerUpdateFailed";
        public const string ManagerSelfSelection = Prefix + "users.managerSelfSelection";
        public const string ManagerNotFound = Prefix + "users.managerNotFound";
        public const string AccountExpirationUpdateFailed = Prefix + "users.accountExpirationUpdateFailed";
        public const string AccountExpirationInvalidDate = Prefix + "users.accountExpirationInvalidDate";
        public const string AccountOperationFailed = Prefix + "users.accountOperationFailed";
        public const string AccountEnabled = Prefix + "users.accountEnabled";
        public const string AccountDisabled = Prefix + "users.accountDisabled";
        public const string AccountUnlocked = Prefix + "users.accountUnlocked";
        public const string AccountAlreadyEnabled = Prefix + "users.accountAlreadyEnabled";
        public const string AccountAlreadyDisabled = Prefix + "users.accountAlreadyDisabled";
        public const string AccountNotLocked = Prefix + "users.accountNotLocked";
        public const string GroupOperationFailed = Prefix + "users.groupOperationFailed";
        public const string GroupMembershipAdded = Prefix + "users.groupMembershipAdded";
        public const string GroupMembershipRemoved = Prefix + "users.groupMembershipRemoved";
        public const string AlreadyInGroup = Prefix + "users.alreadyInGroup";
        public const string NotInGroup = Prefix + "users.notInGroup";
        public const string EffectiveGroupsFailed = Prefix + "users.effectiveGroupsFailed";
        public const string EffectiveGroupsMaxDepthOutOfRange = Prefix + "users.effectiveGroupsMaxDepthOutOfRange";
        public const string InvalidUserId = Prefix + "users.invalidUserId";
        public const string GivenNameRequired = Prefix + "users.givenNameRequired";
        public const string SurnameRequired = Prefix + "users.surnameRequired";
        public const string DisplayNameRequired = Prefix + "users.displayNameRequired";
        public const string InitialPasswordRequired = Prefix + "users.initialPasswordRequired";
        public const string SamAccountNameRequired = Prefix + "users.samAccountNameRequired";
        public const string SamAccountNameTooLong = Prefix + "users.samAccountNameTooLong";
        public const string SamAccountNameInvalidCharacters = Prefix + "users.samAccountNameInvalidCharacters";
        public const string UpnRequired = Prefix + "users.upnRequired";
        public const string UpnInvalid = Prefix + "users.upnInvalid";
    }

    public static class Groups
    {
        public const string NotFound = Prefix + "groups.notFound";
        public const string QueryFailed = Prefix + "groups.queryFailed";
        public const string CreateSuccess = Prefix + "groups.createSuccess";
        public const string CreateFailed = Prefix + "groups.createFailed";
        public const string UpdateFailed = Prefix + "groups.updateFailed";
        public const string DeleteSuccess = Prefix + "groups.deleteSuccess";
        public const string DeleteFailed = Prefix + "groups.deleteFailed";
        public const string OuMoveSuccess = Prefix + "groups.ouMoveSuccess";
        public const string OuMoveFailed = Prefix + "groups.ouMoveFailed";
        public const string TargetOuRequired = Prefix + "groups.targetOuRequired";
        public const string AlreadyInTargetOu = Prefix + "groups.alreadyInTargetOu";
        public const string InvalidTargetOu = Prefix + "groups.invalidTargetOu";
        public const string MemberOperationFailed = Prefix + "groups.memberOperationFailed";
        public const string MemberAdded = Prefix + "groups.memberAdded";
        public const string MemberRemoved = Prefix + "groups.memberRemoved";
        public const string MemberAlreadyInGroup = Prefix + "groups.memberAlreadyInGroup";
        public const string MemberNotInGroup = Prefix + "groups.memberNotInGroup";
        public const string SelfMembership = Prefix + "groups.selfMembership";
        public const string InvalidGroupId = Prefix + "groups.invalidGroupId";
        public const string GroupDnRequired = Prefix + "groups.groupDnRequired";
        public const string DisplayNameRequired = Prefix + "groups.displayNameRequired";
        public const string TechnicalNameRequired = Prefix + "groups.technicalNameRequired";
        public const string GroupScopeRequired = Prefix + "groups.groupScopeRequired";
        public const string InvalidGroupScope = Prefix + "groups.invalidGroupScope";
        public const string SamAccountNameRequired = Prefix + "groups.samAccountNameRequired";
        public const string SamAccountNameTooLong = Prefix + "groups.samAccountNameTooLong";
        public const string SamAccountNameInvalidCharacters = Prefix + "groups.samAccountNameInvalidCharacters";
    }

    public static class Computers
    {
        public const string NotFound = Prefix + "computers.notFound";
        public const string QueryFailed = Prefix + "computers.queryFailed";
        public const string UpdateFailed = Prefix + "computers.updateFailed";
        public const string DeleteSuccess = Prefix + "computers.deleteSuccess";
        public const string DeleteFailed = Prefix + "computers.deleteFailed";
        public const string OuMoveSuccess = Prefix + "computers.ouMoveSuccess";
        public const string OuMoveFailed = Prefix + "computers.ouMoveFailed";
        public const string TargetOuRequired = Prefix + "computers.targetOuRequired";
        public const string AlreadyInTargetOu = Prefix + "computers.alreadyInTargetOu";
        public const string InvalidTargetOu = Prefix + "computers.invalidTargetOu";
        public const string AccountOperationFailed = Prefix + "computers.accountOperationFailed";
        public const string AccountEnabled = Prefix + "computers.accountEnabled";
        public const string AccountDisabled = Prefix + "computers.accountDisabled";
        public const string GroupOperationFailed = Prefix + "computers.groupOperationFailed";
        public const string GroupMembershipAdded = Prefix + "computers.groupMembershipAdded";
        public const string GroupMembershipRemoved = Prefix + "computers.groupMembershipRemoved";
        public const string AlreadyInGroup = Prefix + "computers.alreadyInGroup";
        public const string NotInGroup = Prefix + "computers.notInGroup";
        public const string ProtectedDelete = Prefix + "computers.protectedDelete";
        public const string ProtectedWrite = Prefix + "computers.protectedWrite";
        public const string InvalidComputerId = Prefix + "computers.invalidComputerId";
    }

    public static class DeletedObjects
    {
        public const string NotFound = Prefix + "deletedObjects.notFound";
        public const string QueryFailed = Prefix + "deletedObjects.queryFailed";
        public const string AccessDenied = Prefix + "deletedObjects.accessDenied";
        public const string RestoreSuccess = Prefix + "deletedObjects.restoreSuccess";
        public const string RestoreFailed = Prefix + "deletedObjects.restoreFailed";
        public const string RestoreUnsupportedType = Prefix + "deletedObjects.restoreUnsupportedType";
        public const string RestoreMissingTarget = Prefix + "deletedObjects.restoreMissingTarget";
        public const string RestoreTargetNotFound = Prefix + "deletedObjects.restoreTargetNotFound";
        public const string RestoreConflict = Prefix + "deletedObjects.restoreConflict";
        public const string RestorePowerShellModuleMissing = Prefix + "deletedObjects.restorePowerShellModuleMissing";
        public const string RestoreParentNotFound = Prefix + "deletedObjects.restoreParentNotFound";
        public const string RestoreTargetOuNotFound = Prefix + "deletedObjects.restoreTargetOuNotFound";
    }

    public static class OrganizationalUnits
    {
        public const string NotFound = Prefix + "organizationalUnits.notFound";
        public const string QueryFailed = Prefix + "organizationalUnits.queryFailed";
        public const string CreateSuccess = Prefix + "organizationalUnits.createSuccess";
        public const string CreateFailed = Prefix + "organizationalUnits.createFailed";
        public const string RenameSuccess = Prefix + "organizationalUnits.renameSuccess";
        public const string RenameFailed = Prefix + "organizationalUnits.renameFailed";
        public const string MoveSuccess = Prefix + "organizationalUnits.moveSuccess";
        public const string MoveFailed = Prefix + "organizationalUnits.moveFailed";
        public const string DeleteSuccess = Prefix + "organizationalUnits.deleteSuccess";
        public const string DeleteFailed = Prefix + "organizationalUnits.deleteFailed";
        public const string NameRequired = Prefix + "organizationalUnits.nameRequired";
        public const string NameTooLong = Prefix + "organizationalUnits.nameTooLong";
        public const string NameInvalidCharacters = Prefix + "organizationalUnits.nameInvalidCharacters";
        public const string ParentRequired = Prefix + "organizationalUnits.parentRequired";
        public const string InvalidParent = Prefix + "organizationalUnits.invalidParent";
        public const string TargetParentRequired = Prefix + "organizationalUnits.targetParentRequired";
        public const string InvalidTargetParent = Prefix + "organizationalUnits.invalidTargetParent";
        public const string AlreadyInTargetParent = Prefix + "organizationalUnits.alreadyInTargetParent";
        public const string NameCollision = Prefix + "organizationalUnits.nameCollision";
        public const string ProtectedObject = Prefix + "organizationalUnits.protectedObject";
        public const string NotEmpty = Prefix + "organizationalUnits.notEmpty";
        public const string InvalidMoveTarget = Prefix + "organizationalUnits.invalidMoveTarget";
        public const string InvalidOrganizationalUnitId = Prefix + "organizationalUnits.invalidOrganizationalUnitId";
    }

    public static class OperationFailures
    {
        public const string PreflightSamAccountNameDuplicate = Prefix + "operationFailures.preflightSamAccountNameDuplicate";
        public const string PreflightUpnDuplicate = Prefix + "operationFailures.preflightUpnDuplicate";
        public const string PreflightCnDuplicate = Prefix + "operationFailures.preflightCnDuplicate";
        public const string PreflightGroupSamAccountNameDuplicate = Prefix + "operationFailures.preflightGroupSamAccountNameDuplicate";
        public const string PreflightGroupCnDuplicate = Prefix + "operationFailures.preflightGroupCnDuplicate";
    }

    public static class Ldap
    {
        public const string EntryAlreadyExists = Prefix + "ldap.entryAlreadyExists";
        public const string ConstraintViolation = Prefix + "ldap.constraintViolation";
        public const string InvalidDnSyntax = Prefix + "ldap.invalidDnSyntax";
        public const string InsufficientAccessRights = Prefix + "ldap.insufficientAccessRights";
        public const string UnwillingToPerform = Prefix + "ldap.unwillingToPerform";
        public const string NoSuchObject = Prefix + "ldap.noSuchObject";
        public const string ConnectionFailed = Prefix + "ldap.connectionFailed";
        public const string UpdateUserFailed = Prefix + "ldap.updateUserFailed";
        public const string UpdateGroupFailed = Prefix + "ldap.updateGroupFailed";
        public const string CreateGroupFailed = Prefix + "ldap.createGroupFailed";
        public const string DeleteGroupFailed = Prefix + "ldap.deleteGroupFailed";
        public const string GroupNotFound = Prefix + "ldap.groupNotFound";
    }
}
