namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapFilterHelper
{
    public static string EscapeFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }

    public static string FormatObjectGuidFilter(Guid objectGuid)
    {
        var bytes = objectGuid.ToByteArray();
        Span<char> buffer = stackalloc char[bytes.Length * 3];
        var pos = 0;
        foreach (var b in bytes)
        {
            buffer[pos++] = '\\';
            buffer[pos++] = ToHexChar(b >> 4);
            buffer[pos++] = ToHexChar(b & 0x0F);
        }

        return new string(buffer[..pos]);

        static char ToHexChar(int value) => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
    }
}
