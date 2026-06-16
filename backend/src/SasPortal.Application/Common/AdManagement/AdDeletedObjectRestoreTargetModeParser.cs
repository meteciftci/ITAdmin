using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdDeletedObjectRestoreTargetModeParser
{
    public static bool TryParse(
        string? value,
        out AdDeletedObjectRestoreTargetMode restoreTargetMode)
    {
        restoreTargetMode = AdDeletedObjectRestoreTargetMode.OriginalLocation;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out restoreTargetMode);
    }
}
