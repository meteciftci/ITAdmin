namespace SasPortal.Application.Common.AdManagement;

public sealed record AdUserUpdateFailureContext(
    string Step,
    string? AttributeName = null,
    int? LdapResultCode = null,
    int? LdapExceptionErrorCode = null,
    string? LdapDiagnosticMessage = null,
    Guid? TargetObjectGuid = null,
    string? TargetDistinguishedName = null,
    string? NormalizedReasonOverride = null,
    string? EnglishMessageOverride = null,
    string? DiagnosticCode = null);
