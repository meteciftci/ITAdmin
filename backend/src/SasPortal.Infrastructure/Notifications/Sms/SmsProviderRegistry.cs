using SasPortal.Application.Abstractions.Notifications;

namespace SasPortal.Infrastructure.Notifications.Sms;

public sealed class SmsProviderRegistry(IEnumerable<ISmsProviderAdapter> adapters) : ISmsProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ISmsProviderAdapter> _adapters =
        adapters.ToDictionary(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ISmsProviderAdapter> GetProviders() => _adapters.Values.ToList();

    public ISmsProviderAdapter GetRequired(string providerKey)
    {
        if (_adapters.TryGetValue(providerKey, out var adapter))
        {
            return adapter;
        }

        throw new InvalidOperationException($"SMS provider '{providerKey}' is not registered.");
    }
}
