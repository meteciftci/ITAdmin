using SasPortal.Application.Common.Notifications;

namespace SasPortal.UnitTests.Notifications;

public sealed class NotificationRecipientMaskerTests
{
    [Fact]
    public void MaskPhone_MasksMiddleDigits()
    {
        var masked = NotificationRecipientMasker.MaskPhone("+905551234567");
        Assert.StartsWith("+905", masked);
        Assert.Contains("*", masked);
        Assert.DoesNotContain("1234567", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskEmail_MasksLocalPart()
    {
        var masked = NotificationRecipientMasker.MaskEmail("mete@mugla.bel.tr");
        Assert.Equal("m***e@mugla.bel.tr", masked);
    }
}
