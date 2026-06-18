namespace ITAdmin.Application.Abstractions.Notifications;

public interface IEmailProviderRegistry
{
    IReadOnlyList<IEmailProviderAdapter> GetProviders();
    IEmailProviderAdapter GetRequired(string providerKey);
}
