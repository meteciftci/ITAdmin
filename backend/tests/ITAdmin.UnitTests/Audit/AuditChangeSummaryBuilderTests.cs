using ITAdmin.Application.Common.Audit;

namespace ITAdmin.UnitTests.Audit;

public sealed class AuditChangeSummaryBuilderTests
{
    [Fact]
    public void BuildUpdateDescription_PublicField_WritesOldNew()
    {
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(
            "Notification provider settings updated. Channel: Sms. Provider: CustomHttp.",
            [
                AuditChangeSummaryBuilder.PublicField("Host", "smtp-old.local", "smtp.mugla.bel.tr"),
            ]);

        Assert.Contains("Host smtp-old.local -> smtp.mugla.bel.tr", description, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUpdateDescription_SensitiveField_DoesNotExposeValues()
    {
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(
            "Notification provider settings updated. Channel: Email. Provider: Smtp.",
            [AuditChangeSummaryBuilder.SensitiveChanged("Password", hadValue: true, hasValue: true)]);

        Assert.Contains("Password changed", description, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUpdateDescription_LongField_WritesChangedOnly()
    {
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(
            "Prefix",
            [
                AuditChangeSummaryBuilder.PublicField(
                    "BodyTemplate",
                    new string('a', 150),
                    new string('b', 150),
                    treatAsLongText: true),
            ]);

        Assert.Contains("BodyTemplate changed", description, StringComparison.Ordinal);
        Assert.DoesNotContain("aaa", description, StringComparison.Ordinal);
    }
}
