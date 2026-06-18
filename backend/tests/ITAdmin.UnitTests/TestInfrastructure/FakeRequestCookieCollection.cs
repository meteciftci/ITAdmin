using Microsoft.AspNetCore.Http;

namespace ITAdmin.UnitTests.TestInfrastructure;

/// <summary>
/// Minimal IRequestCookieCollection stub for exercising cookie-based HTTP logic in tests.
/// </summary>
public sealed class FakeRequestCookieCollection : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _inner;

    public FakeRequestCookieCollection(Dictionary<string, string>? inner = null)
    {
        _inner = inner ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string? this[string key] => _inner.TryGetValue(key, out var v) ? v : null;

    public ICollection<string> Keys => _inner.Keys;

    public int Count => _inner.Count;

    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _inner.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGetValue(string key, out string value)
    {
        if (_inner.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
