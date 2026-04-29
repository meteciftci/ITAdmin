namespace SasPortal.Domain.Enums;

public enum SecurityEventType
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    Logout = 3,
    RefreshTokenIssued = 4,
    RefreshTokenRevoked = 5,
    AccessDenied = 6,
    PermissionDenied = 7,
    SetupCompleted = 8,
    LdapValidationSucceeded = 9,
    LdapValidationFailed = 10
}
