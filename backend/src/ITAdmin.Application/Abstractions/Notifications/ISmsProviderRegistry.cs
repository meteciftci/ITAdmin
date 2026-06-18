namespace ITAdmin.Application.Abstractions.Notifications;

public interface ISmsProviderRegistry
{
    IReadOnlyList<ISmsProviderAdapter> GetProviders();
    ISmsProviderAdapter GetRequired(string providerKey);
}
