using SasPortal.Application.Common.Constants;

namespace SasPortal.Application.Common.AdManagement;

public static class AdManagementNotificationSettingsAuditBuilder
{
    public static string BuildRulesChangeSummary(
        AdManagementNotificationSettings before,
        AdManagementNotificationSettings after)
    {
        var changes = new List<string>();
        var beforeRules = IndexRules(before.Rules);
        var afterRules = IndexRules(after.Rules);

        foreach (var (key, afterRule) in afterRules)
        {
            if (!beforeRules.TryGetValue(key, out var beforeRule))
            {
                changes.Add(
                    $"{FormatRuleKey(afterRule)} added{(afterRule.IsEnabled ? string.Empty : " (passive)")}");
                continue;
            }

            if (!beforeRule.IsEnabled && afterRule.IsEnabled)
            {
                changes.Add($"{FormatRuleKey(afterRule)} enabled");
            }
            else if (beforeRule.IsEnabled && !afterRule.IsEnabled)
            {
                changes.Add($"{FormatRuleKey(afterRule)} disabled");
            }

            if (!RecipientSourcesEqual(beforeRule.RecipientSource, afterRule.RecipientSource))
            {
                changes.Add($"{FormatRuleKey(afterRule)} recipient updated");
            }
        }

        foreach (var (key, beforeRule) in beforeRules)
        {
            if (!afterRules.ContainsKey(key))
            {
                changes.Add($"{FormatRuleKey(beforeRule)} removed");
            }
        }

        if (changes.Count == 0)
        {
            return string.Empty;
        }

        return $"AD management notification rules updated. Changes: {string.Join(", ", changes)}.";
    }

    public static string BuildRuleAddedSummary(AdManagementNotificationRule rule) =>
        $"AD management notification rule added. Event: {rule.EventKey}. Channel: {rule.Channel}.";

    public static string BuildRuleRemovedSummary(AdManagementNotificationRule rule) =>
        $"AD management notification rule removed. Event: {rule.EventKey}. Channel: {rule.Channel}.";

    public static string BuildRuleUpdatedSummary(
        AdManagementNotificationRule before,
        AdManagementNotificationRule after)
    {
        var changes = new List<string>();
        if (before.IsEnabled != after.IsEnabled)
        {
            changes.Add($"IsEnabled {before.IsEnabled} -> {after.IsEnabled}");
        }

        if (!RecipientSourcesEqual(before.RecipientSource, after.RecipientSource))
        {
            changes.Add("RecipientSource updated");
        }

        if (changes.Count == 0)
        {
            return $"AD management notification rule updated. Event: {after.EventKey}. Channel: {after.Channel}.";
        }

        return
            $"AD management notification rule updated. Event: {after.EventKey}. Channel: {after.Channel}. Changes: {string.Join(", ", changes)}.";
    }

    private static Dictionary<string, AdManagementNotificationRule> IndexRules(
        IEnumerable<AdManagementNotificationRule>? rules)
    {
        var index = new Dictionary<string, AdManagementNotificationRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules ?? [])
        {
            var key = BuildRuleKey(rule.EventKey, rule.Channel);
            index[key] = rule;
        }

        return index;
    }

    private static string BuildRuleKey(string eventKey, string channel) =>
        $"{eventKey.Trim()}|{channel.Trim()}";

    private static string FormatRuleKey(AdManagementNotificationRule rule) =>
        $"{rule.EventKey}/{rule.Channel}";

    private static bool RecipientSourcesEqual(
        AdManagementNotificationRecipientSource? before,
        AdManagementNotificationRecipientSource? after)
    {
        var beforeType = before?.Type?.Trim() ?? string.Empty;
        var afterType = after?.Type?.Trim() ?? string.Empty;
        if (!string.Equals(beforeType, afterType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var beforeValue = before?.Value?.Trim() ?? string.Empty;
        var afterValue = after?.Value?.Trim() ?? string.Empty;
        return string.Equals(beforeValue, afterValue, StringComparison.OrdinalIgnoreCase);
    }
}
