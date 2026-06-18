using SasPortal.Application.Common.Security;

namespace SasPortal.Application.Common.Constants;

public static class NotificationOutboxPermissions
{
    public const string View = PermissionCodes.NotificationOutbox.View;
    public const string Retry = PermissionCodes.NotificationOutbox.Retry;
    public const string Cancel = PermissionCodes.NotificationOutbox.Cancel;
}
