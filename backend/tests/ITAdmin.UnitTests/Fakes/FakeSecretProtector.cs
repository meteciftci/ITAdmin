using ITAdmin.Application.Abstractions.Security;

namespace ITAdmin.UnitTests.Fakes;

public sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plainText) => $"protected:{plainText}";

    public string Unprotect(string protectedText) => protectedText.Replace("protected:", string.Empty, StringComparison.Ordinal);
}
