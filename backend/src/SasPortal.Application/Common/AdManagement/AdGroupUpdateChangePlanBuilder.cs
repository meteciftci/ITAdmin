using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdGroupUpdateChangePlanBuilder
{
    public const string StepUpdateBasicAttribute = "UpdateBasicAttribute";
    public const string StepRenameCn = "RenameCn";
    public const string StepPreflight = "Preflight";

    public static AdGroupUpdateChangePlan Build(
        UpdateAdGroupRequest request,
        IReadOnlyDictionary<string, string?> currentScalars,
        string currentDistinguishedName)
    {
        var currentCommonName =
            AdLdapDnHelper.ParseCommonNameFromDistinguishedName(currentDistinguishedName)
            ?? string.Empty;
        var requestedCommonName = AdGroupNameNormalizer.NormalizeTechnicalName(request.Name);
        var parentDn = AdLdapDnHelper.GetParentDistinguishedName(currentDistinguishedName);
        var requiresRename = !string.Equals(
            currentCommonName,
            requestedCommonName,
            StringComparison.OrdinalIgnoreCase);

        var scalarChanges = new List<AdGroupUpdateScalarChange>();

        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "displayName", request.DisplayName);
        AddScalarReplaceIfChanged(scalarChanges, currentScalars, "sAMAccountName", request.SamAccountName);
        AddOptionalScalarChange(scalarChanges, currentScalars, "description", request.Description);

        AdGroupUpdateRenameChange? renameChange = null;
        if (requiresRename && !string.IsNullOrWhiteSpace(parentDn))
        {
            renameChange = new AdGroupUpdateRenameChange(
                currentCommonName,
                requestedCommonName,
                parentDn,
                currentDistinguishedName);
        }

        return new AdGroupUpdateChangePlan
        {
            GroupObjectGuid = request.GroupId,
            CurrentDistinguishedName = currentDistinguishedName,
            CurrentCommonName = currentCommonName,
            RequestedCommonName = requestedCommonName,
            RequiresRename = requiresRename && renameChange is not null,
            ParentDistinguishedName = parentDn,
            ScalarChanges = scalarChanges,
            RenameChange = renameChange,
        };
    }

    private static void AddScalarReplaceIfChanged(
        ICollection<AdGroupUpdateScalarChange> changes,
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
            new AdGroupUpdateScalarChange(
                attributeName,
                StepUpdateBasicAttribute,
                AdUserUpdateScalarChangeKind.Replace,
                [requestedValue],
                existing is null ? [] : [existing]));
    }

    private static void AddOptionalScalarChange(
        ICollection<AdGroupUpdateScalarChange> changes,
        IReadOnlyDictionary<string, string?> currentScalars,
        string attributeName,
        string? requestedValue)
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
                    new AdGroupUpdateScalarChange(
                        attributeName,
                        StepUpdateBasicAttribute,
                        AdUserUpdateScalarChangeKind.Delete,
                        [],
                        existingValues));
                return;
            case AdMappedAttributeLdapAction.Replace:
                changes.Add(
                    new AdGroupUpdateScalarChange(
                        attributeName,
                        StepUpdateBasicAttribute,
                        AdUserUpdateScalarChangeKind.Replace,
                        [requestedValue!.Trim()],
                        existingValues));
                return;
            default:
                return;
        }
    }
}
