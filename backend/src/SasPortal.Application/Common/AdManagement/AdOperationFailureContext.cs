namespace SasPortal.Application.Common.AdManagement;

public sealed record AdOperationFailureContext(
    string Operation,
    string Step,
    string? DiagnosticCode = null,
    string? NormalizedReasonOverride = null,
    string? EnglishMessageOverride = null,
    int? LdapResultCode = null,
    int? LdapExceptionErrorCode = null,
    string? LdapDiagnosticMessage = null,
    Guid? TargetObjectGuid = null,
    string? TargetDistinguishedName = null,
    bool? PartialUpdate = null,
    string? RollbackStatus = null);
