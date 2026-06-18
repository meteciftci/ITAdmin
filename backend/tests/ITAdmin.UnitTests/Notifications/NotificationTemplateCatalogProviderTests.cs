using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Notifications;

namespace ITAdmin.UnitTests.Notifications;

public sealed class NotificationTemplateCatalogProviderTests
{
    private readonly INotificationTemplateCatalogProvider _provider = new StaticNotificationTemplateCatalogProvider();

    [Fact]
    public void GetCatalog_ContainsSystem_GenericNotification()
    {
        var catalog = _provider.GetCatalog();

        var system = catalog.Modules.Single(m => m.Key == "System");
        var generic = system.Events.Single(e => e.Key == "GenericNotification");

        Assert.Contains(NotificationChannels.Sms, generic.SupportedChannels);
        Assert.Contains(NotificationChannels.Email, generic.SupportedChannels);
    }

    [Fact]
    public void GetCatalog_ContainsAdManagement_UserCreated_WithExpectedVariables()
    {
        var catalog = _provider.GetCatalog();

        var adManagement = catalog.Modules.Single(m => m.Key == "AdManagement");
        var userCreated = adManagement.Events.Single(e => e.Key == "UserCreated");
        var variableKeys = userCreated.Variables.Select(v => v.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("displayName", variableKeys);
        Assert.Contains("username", variableKeys);
        Assert.Contains("upn", variableKeys);
        Assert.DoesNotContain("password", variableKeys);
        Assert.DoesNotContain("temporaryPassword", variableKeys);
    }

    [Fact]
    public void ValidateTemplateKeys_UnknownModule_ReturnsError()
    {
        var error = _provider.ValidateTemplateKeys("Unknown", "UserCreated", NotificationChannels.Sms);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTemplateKeys_UnsupportedChannel_ReturnsError()
    {
        Assert.True(_provider.TryGetEvent("System", "GenericNotification", out var catalogEvent));
        var unsupported = catalogEvent!.SupportedChannels.First() == NotificationChannels.Sms
            ? "Invalid"
            : NotificationChannels.Sms;

        var error = _provider.ValidateTemplateKeys("System", "GenericNotification", unsupported);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTemplateKeys_ValidEvent_ReturnsNull()
    {
        var error = _provider.ValidateTemplateKeys(
            "AdManagement",
            "UserCreated",
            NotificationChannels.Email);

        Assert.Null(error);
    }
}
