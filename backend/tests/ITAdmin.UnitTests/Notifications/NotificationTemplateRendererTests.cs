using ITAdmin.Application.Notifications;

namespace ITAdmin.UnitTests.Notifications;

public sealed class NotificationTemplateRendererTests
{
    private readonly NotificationTemplateRenderer _renderer = new();

    [Fact]
    public void Render_ReplacesKnownVariables()
    {
        var result = _renderer.Render(
            "Merhaba {{displayName}}, hesabınız oluşturuldu.",
            new Dictionary<string, object?> { ["displayName"] = "Çağrı IŞIK" });

        Assert.Equal("Merhaba Çağrı IŞIK, hesabınız oluşturuldu.", result);
    }

    [Fact]
    public void Render_UnknownVariable_ReturnsEmpty()
    {
        var result = _renderer.Render("Hello {{unknown}}", new Dictionary<string, object?>());
        Assert.Equal("Hello ", result);
    }

    [Fact]
    public void Render_NullVariable_ReturnsEmpty()
    {
        var result = _renderer.Render(
            "Value: {{name}}",
            new Dictionary<string, object?> { ["name"] = null });

        Assert.Equal("Value: ", result);
    }

    [Fact]
    public void Render_ReplacesSamePlaceholderMultipleTimes()
    {
        var result = _renderer.Render(
            "{{name}} - {{name}}",
            new Dictionary<string, object?> { ["name"] = "Test" });

        Assert.Equal("Test - Test", result);
    }

    [Fact]
    public void ExtractVariables_ReturnsDistinctNames()
    {
        var variables = _renderer.ExtractVariables("Hi {{displayName}}, code {{code}} and {{displayName}}.");
        Assert.Equal(["displayName", "code"], variables);
    }
}
