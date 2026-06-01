using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUserUpdateChangePlanBuilder
{
    public const string StepUpdateBasicAttribute = "UpdateBasicAttribute";
    public const string StepUpdateMappedAttribute = "UpdateMappedAttribute";
    public const string StepRenameCn = "RenameCn";
    public const string StepPreflight = "Preflight";

    public static AdUserUpdateChangePlan Build(
        UpdateAdUserRequest request,
        IReadOnlyDictionary<string, string?> currentScalars,
        IReadOnlyDictionary<string, IReadOnlyList<string>> currentMappedValuesByAttribute,
        string currentDistinguishedName,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var currentCommonName =
            AdLdapDnHelper.ParseCommonNameFromDistinguishedName(currentDistinguishedName)
            ?? string.Empty;
        var requestedCommonName = AdUpdateUserRequestValidator.DeriveCommonNameFromDisplayName(request.DisplayName);
        var parentDn = AdLdapDnHelper.GetParentDistinguishedName(currentDistinguishedName);
        var requiresRename = !string.Equals(
            currentCommonName,
            requestedCommonName,
            StringComparison.OrdinalIgnoreCase);

        var scalarChanges = new List<AdUserUpdateScalarChange>();

        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "givenName", request.GivenName);
        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "sn", request.Surname);
        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "displayName", request.DisplayName);
        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "sAMAccountName", request.SamAccountName);
        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "userPrincipalName", request.UserPrincipalName);

        if (request.Mail is not null)
        {
            AddOptionalScalarChange(
                scalarChanges,
                currentScalars,
                "mail",
                request.Mail);
        }

        if (request.Department is not null)
        {
            AddOptionalScalarChange(
                scalarChanges,
                currentScalars,
                "department",
                request.Department);
        }

        var mappedChanges = BuildMappedChanges(
            request.MappedAttributes,
            mappings,
            currentMappedValuesByAttribute);

        AdUserUpdateRenameChange? renameChange = null;
        if (requiresRename && !string.IsNullOrWhiteSpace(parentDn))
        {
            renameChange = new AdUserUpdateRenameChange(
                currentCommonName,
                requestedCommonName,
                parentDn,
                currentDistinguishedName);
        }

        return new AdUserUpdateChangePlan
        {
            UserObjectGuid = request.UserId,
            CurrentDistinguishedName = currentDistinguishedName,
            CurrentCommonName = currentCommonName,
            RequestedCommonName = requestedCommonName,
            RequiresRename = requiresRename && renameChange is not null,
            ParentDistinguishedName = parentDn,
            ScalarChanges = scalarChanges,
            MappedChanges = mappedChanges,
            RenameChange = renameChange,
        };
    }

    private static void AddScalarReplaceIfChanged(
        ICollection<AdUserUpdateScalarChange> changes,
        IReadOnlyDictionary<string, string?> currentScalars,
        string attributeName,
        string requestedValue)
    {
        currentScalars.TryGetValue(attributeName, out var existing);
        if (!AdScalarAttributeComparer.HasChanged(existing, requestedValue))
        {
            return;
        }

        changes.Add(
            new AdUserUpdateScalarChange(
                attributeName,
                StepUpdateBasicAttribute,
                AdUserUpdateScalarChangeKind.Replace,
                [requestedValue],
                existing is null ? [] : [existing]));
    }

    private static void AddOptionalScalarChange(
        ICollection<AdUserUpdateScalarChange> changes,
        IReadOnlyDictionary<string, string?> currentScalars,
        string attributeName,
        string requestedValue)
    {
        currentScalars.TryGetValue(attributeName, out var existing);
        var existingValues = string.IsNullOrWhiteSpace(existing) ? [] : new[] { existing! };
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(requestedValue, existingValues);

        switch (action)
        {
            case AdMappedAttributeLdapAction.Skip:
                return;
            case AdMappedAttributeLdapAction.Delete:
                changes.Add(
                    new AdUserUpdateScalarChange(
                        attributeName,
                        StepUpdateBasicAttribute,
                        AdUserUpdateScalarChangeKind.Delete,
                        [],
                        existingValues));
                return;
            case AdMappedAttributeLdapAction.Replace:
                changes.Add(
                    new AdUserUpdateScalarChange(
                        attributeName,
                        StepUpdateBasicAttribute,
                        AdUserUpdateScalarChangeKind.Replace,
                        [requestedValue.Trim()],
                        existingValues));
                return;
            default:
                return;
        }
    }

    private static List<AdUserUpdateMappedChange> BuildMappedChanges(
        IReadOnlyList<UpdateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        IReadOnlyDictionary<string, IReadOnlyList<string>> currentMappedValuesByAttribute)
    {
        var changes = new List<AdUserUpdateMappedChange>();
        var editableMappings = mappings
            .Where(static mapping =>
                mapping.IsEnabled
                && mapping.IsEditable
                && !AdReservedCoreAttributes.IsReserved(mapping.AttributeName))
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        foreach (var mappedAttribute in mappedAttributes)
        {
            if (!editableMappings.TryGetValue(mappedAttribute.LogicalField, out var mapping))
            {
                continue;
            }

            currentMappedValuesByAttribute.TryGetValue(
                mapping.AttributeName,
                out var existingValues);
            existingValues ??= [];

            var requestedValue = AdMappedAttributeValueExtractor.ExtractScalar(mappedAttribute.Value);
            var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(requestedValue, existingValues);

            switch (action)
            {
                case AdMappedAttributeLdapAction.Skip:
                    continue;
                case AdMappedAttributeLdapAction.Delete:
                    changes.Add(
                        new AdUserUpdateMappedChange(
                            mapping.LogicalField,
                            mapping.AttributeName,
                            StepUpdateMappedAttribute,
                            AdUserUpdateScalarChangeKind.Delete,
                            [],
                            existingValues.ToArray()));
                    continue;
                case AdMappedAttributeLdapAction.Replace:
                    changes.Add(
                        new AdUserUpdateMappedChange(
                            mapping.LogicalField,
                            mapping.AttributeName,
                            StepUpdateMappedAttribute,
                            AdUserUpdateScalarChangeKind.Replace,
                            [requestedValue!],
                            existingValues.ToArray()));
                    continue;
                default:
                    continue;
            }
        }

        return changes;
    }
}
