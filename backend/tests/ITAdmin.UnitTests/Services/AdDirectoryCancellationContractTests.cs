using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ITAdmin.UnitTests.Services;

/// <summary>
/// Source-level invariants for cancellation in the AD directory operations.
///
/// These are deliberately structural. The behaviour they protect — a cancelled request stops
/// contacting domain controllers, and never surfaces to the operator as "domain controller
/// unavailable" — spans roughly 46 operations, and the individual operations need a live
/// directory to exercise end to end. Asserting the contract at the source level keeps every
/// current and future operation honest without a live domain.
/// </summary>
public sealed class AdDirectoryCancellationContractTests
{
    private static IReadOnlyList<string> DirectoryServiceFiles()
    {
        var directory = Path.Combine(RepositoryRoot(), "backend", "src", "ITAdmin.Infrastructure", "Services");
        var files = Directory.GetFiles(directory, "AdUserDirectoryService*.cs");
        Assert.NotEmpty(files);
        return files;
    }

    private static string RepositoryRoot([CallerFilePath] string callerFilePath = "")
    {
        // <root>/backend/tests/ITAdmin.UnitTests/Services/<this file>
        var directory = new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    [Fact]
    public void EveryDirectoryOperation_PassesItsCancellationTokenIntoTheFailoverBind()
    {
        var offenders = new List<string>();

        foreach (var file in DirectoryServiceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("CreateBoundConnection(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!lines[index].Contains("cancellationToken", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These CreateBoundConnection call sites drop the request's CancellationToken, so a "
                + "cancelled request would still wait out the bind timeout on every configured "
                + $"domain controller: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EveryDirectoryOperation_RethrowsCancellationBeforeClassifyingDirectoryFailures()
    {
        var methodLevelCatch = new Regex(@"^        catch \(", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in DirectoryServiceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("CreateBoundConnection(", StringComparison.Ordinal))
                {
                    continue;
                }

                // The first method-level catch clause after the bind must be the cancellation
                // rethrow; anything else would convert a cancelled request into a domain error.
                var found = false;
                for (var scan = index + 1; scan < lines.Length; scan++)
                {
                    if (!methodLevelCatch.IsMatch(lines[scan]))
                    {
                        continue;
                    }

                    found = lines[scan].Contains("OperationCanceledException", StringComparison.Ordinal);
                    break;
                }

                if (!found)
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These directory operations would classify an OperationCanceledException as an LDAP or "
                + "connection failure instead of letting it propagate, reporting a cancelled request "
                + $"as an unavailable domain controller: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TheContractCoversEveryDirectoryBindSite()
    {
        // Guards the two tests above against silently passing if the call sites are renamed away.
        var siteCount = DirectoryServiceFiles()
            .SelectMany(File.ReadAllLines)
            .Count(line => line.Contains("CreateBoundConnection(", StringComparison.Ordinal));

        Assert.True(siteCount >= 40, $"Expected the directory bind sites to still be present, found {siteCount}.");
    }
}
