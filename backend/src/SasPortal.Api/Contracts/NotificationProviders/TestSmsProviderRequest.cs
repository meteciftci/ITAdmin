namespace SasPortal.Api.Contracts.NotificationProviders;

public sealed class TestSmsProviderRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
