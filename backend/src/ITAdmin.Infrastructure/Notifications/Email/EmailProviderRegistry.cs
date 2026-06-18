using ITAdmin.Application.Abstractions.Notifications;

namespace ITAdmin.Infrastructure.Notifications.Email;

public sealed class EmailProviderRegistry(IEnumerable<IEmailProviderAdapter> adapters) : IEmailProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IEmailProviderAdapter> _adapters =
        adapters.ToDictionary(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IEmailProviderAdapter> GetProviders() => _adapters.Values.ToList();

    public IEmailProviderAdapter GetRequired(string providerKey)
    {
        if (_adapters.TryGetValue(providerKey, out var adapter))
        {
            return adapter;
        }

        throw new InvalidOperationException($"Email provider '{providerKey}' is not registered.");
    }
}
