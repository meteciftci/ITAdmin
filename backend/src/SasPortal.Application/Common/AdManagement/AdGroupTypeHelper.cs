namespace SasPortal.Application.Common.AdManagement;

public enum AdGroupScope
{
    Global,
    DomainLocal,
    Universal,
    Unknown,
}

public sealed record AdGroupTypeInfo(bool SecurityEnabled, AdGroupScope Scope);

public static class AdGroupTypeHelper
{
    private const int GlobalGroupFlag = 0x00000002;
    private const int DomainLocalGroupFlag = 0x00000004;
    private const int UniversalGroupFlag = 0x00000008;
    private const int SecurityEnabledFlag = unchecked((int)0x80000000);

    public static AdGroupTypeInfo Parse(int? groupTypeRaw)
    {
        if (groupTypeRaw is null)
        {
            return new AdGroupTypeInfo(false, AdGroupScope.Unknown);
        }

        var value = groupTypeRaw.Value;
        var securityEnabled = (value & SecurityEnabledFlag) != 0;
        var scope = ResolveScope(value);
        return new AdGroupTypeInfo(securityEnabled, scope);
    }

    public static string ScopeToCode(AdGroupScope scope) =>
        scope switch
        {
            AdGroupScope.Global => "Global",
            AdGroupScope.DomainLocal => "DomainLocal",
            AdGroupScope.Universal => "Universal",
            _ => "Unknown",
        };

    private static AdGroupScope ResolveScope(int groupTypeRaw)
    {
        if ((groupTypeRaw & UniversalGroupFlag) != 0)
        {
            return AdGroupScope.Universal;
        }

        if ((groupTypeRaw & DomainLocalGroupFlag) != 0)
        {
            return AdGroupScope.DomainLocal;
        }

        if ((groupTypeRaw & GlobalGroupFlag) != 0)
        {
            return AdGroupScope.Global;
        }

        return AdGroupScope.Unknown;
    }
}
