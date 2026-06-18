namespace ITAdmin.Application.Common.AdManagement;

public static class AdLdapSidHelper
{
    public static string? FormatObjectSid(byte[]? sidBytes)
    {
        if (sidBytes is null || sidBytes.Length < 8)
        {
            return null;
        }

        try
        {
            var revision = sidBytes[0];
            var subAuthorityCount = sidBytes[1];
            if (sidBytes.Length < 8 + subAuthorityCount * 4)
            {
                return null;
            }

            var identifierAuthority =
                ((long)sidBytes[2] << 40)
                | ((long)sidBytes[3] << 32)
                | ((long)sidBytes[4] << 24)
                | ((long)sidBytes[5] << 16)
                | ((long)sidBytes[6] << 8)
                | sidBytes[7];

            var sid = $"S-{revision}-{identifierAuthority}";
            for (var index = 0; index < subAuthorityCount; index++)
            {
                var offset = 8 + index * 4;
                var subAuthority =
                    sidBytes[offset]
                    | (sidBytes[offset + 1] << 8)
                    | (sidBytes[offset + 2] << 16)
                    | (sidBytes[offset + 3] << 24);
                sid += $"-{subAuthority}";
            }

            return sid;
        }
        catch (Exception)
        {
            // Invalid SID bytes are treated as missing directory metadata.
            return null;
        }
    }
}
