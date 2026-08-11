using System.Reflection;
using ITAdmin.Application.Common.Security;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.Security;

public sealed class PermissionCodesSeedAlignmentTests
{
    [Fact]
    public void PermissionCodes_ContainsAllDefaultSetupPermissionCodes()
    {
        var defaultCodes = GetDefaultSetupPermissionCodes();
        var constantCodes = GetPermissionCodeConstants().ToHashSet(StringComparer.Ordinal);

        var missing = defaultCodes
            .Where(code => !constantCodes.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void DefaultSetupPermissionCodes_ContainEveryDefinedPermissionCode()
    {
        var defaultCodes = GetDefaultSetupPermissionCodes();
        var constantCodes = GetPermissionCodeConstants()
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(constantCodes, defaultCodes);
    }

    private static IReadOnlyList<string> GetDefaultSetupPermissionCodes()
    {
        var field = typeof(SetupService).GetField(
            "DefaultPermissions",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var defaultPermissions = (ValueTuple<string, string, string>[])field!.GetValue(null)!;
        return defaultPermissions
            .Select(item => item.Item2)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> GetPermissionCodeConstants()
    {
        foreach (var value in GetNestedStringConstants(typeof(PermissionCodes)))
        {
            yield return value;
        }
    }

    private static IEnumerable<string> GetNestedStringConstants(Type type)
    {
        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            foreach (var field in nestedType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(string) && field.IsLiteral && !field.IsInitOnly)
                {
                    var value = field.GetRawConstantValue() as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }
            }

            foreach (var value in GetNestedStringConstants(nestedType))
            {
                yield return value;
            }
        }
    }
}
