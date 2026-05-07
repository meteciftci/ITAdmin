using SasPortal.Application.Abstractions.Security;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plainText) => $"protected:{plainText}";

    public string Unprotect(string protectedText) => protectedText.Replace("protected:", string.Empty, StringComparison.Ordinal);
}
