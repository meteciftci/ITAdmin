using ITAdmin.Application.Common.Security;

namespace ITAdmin.Application.Common.Constants;

public static class NotificationOutboxPermissions
{
    public const string View = PermissionCodes.NotificationOutbox.View;
    public const string Retry = PermissionCodes.NotificationOutbox.Retry;
    public const string Cancel = PermissionCodes.NotificationOutbox.Cancel;
}
