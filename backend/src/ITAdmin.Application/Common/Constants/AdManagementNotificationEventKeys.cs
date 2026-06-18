namespace ITAdmin.Application.Common.Constants;

public static class AdManagementNotificationEventKeys
{
    public const string UserCreated = "UserCreated";
    public const string UserEnabled = "UserEnabled";
    public const string UserDisabled = "UserDisabled";
    public const string UserUnlocked = "UserUnlocked";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UserCreated,
        UserEnabled,
        UserDisabled,
        UserUnlocked,
    };
}
