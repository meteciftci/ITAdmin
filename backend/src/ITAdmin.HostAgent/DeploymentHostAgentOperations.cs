using System.Globalization;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// The privileged operations. Not a deployment engine: an update is applied by running the
/// <c>Deploy-ITAdmin.ps1</c> already checked out under <c>&lt;InstallRoot&gt;\src</c>, via the
/// Update Coordinator, with arguments the agent builds from its own configuration. First install
/// and update therefore converge on one script, and a fix to the deployment sequence cannot land in
/// one path and be forgotten in the other.
/// </summary>
public sealed class DeploymentHostAgentOperations(
    HostAgentSettings settings,
    GitSourceClient gitClient,
    IHostDeploymentExecutor executor,
    ILogger<DeploymentHostAgentOperations> logger) : IHostAgentOperations
{
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void ReconcileInterruptedOperation()
    {
        var record = ReadOperation();
        if (record is null || IsTerminal(record.Phase))
        {
            return;
        }

        // A running phase with no live process behind it means the service died mid-update. The
        // build on disk may be half-produced and the schema may be part-migrated; that is an
        // operator-review situation, never a silent retry.
        logger.LogWarning(
            "An update ({OperationId}) targeting {TargetCommit} was in phase {Phase} when the agent last stopped.",
            record.OperationId, record.TargetCommit, record.Phase);

        WriteOperation(record with
        {
            Phase = HostAgentUpdatePhase.RequiresOperatorReview,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Message = "A previous update was interrupted. Review the deployment state and the ITAdmin Host Agent log, "
                      + "then run Deploy-ITAdmin.ps1 on this host to converge.",
        });
    }

    public async Task<HostAgentResponse> GetInstallationStatusAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var state = ReadDeployState();
        var healthy = await IsLocallyHealthyAsync(cancellationToken);

        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Installation status read.",
            CorrelationId = request.CorrelationId,
            Installation = new HostAgentInstallationStatus
            {
                Phase = state?.ActiveSha is null ? "NotInstalled" : "Installed",
                ActiveCommit = state?.ActiveSha,
                PreviousCommit = state?.PreviousSha,
                Branch = settings.Branch,
                BuiltAtUtc = TryParseTimestamp(state?.ActiveBuiltAtUtc),
                Healthy = healthy,
            },
        };
    }

    public async Task<HostAgentResponse> CheckForUpdatesAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var access = await gitClient.DiagnoseAccessAsync(cancellationToken);
        if (!access.IsAccessible)
        {
            return new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Failed,
                Message = access.Message,
                CorrelationId = request.CorrelationId,
                RepositoryStatus = access.Status,
            };
        }

        var availability = await gitClient.GetAvailabilityAsync(cancellationToken);
        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = availability.UpToDate
                ? "The deployed build is at the branch tip."
                : $"{availability.CommitsBehind} commit(s) behind {availability.Branch}.",
            CorrelationId = request.CorrelationId,
            RepositoryStatus = HostAgentRepositoryStatus.Verified,
            Availability = availability,
        };
    }

    public async Task<HostAgentResponse> RequestUpdateAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        if (!settings.UpdatesEnabled)
        {
            return HostAgentResponse.Rejected(
                "Repository-backed updates are disabled on this host (updatesEnabled=false in hostagent.json).",
                request.CorrelationId);
        }

        if (!await _updateGate.WaitAsync(0, cancellationToken))
        {
            return HostAgentResponse.Rejected("An update is already being applied.", request.CorrelationId);
        }

        try
        {
            var existing = ReadOperation();
            if (existing is not null && !IsTerminal(existing.Phase))
            {
                return HostAgentResponse.Rejected("An update is already in progress.", request.CorrelationId);
            }
            if (existing?.Phase is HostAgentUpdatePhase.RequiresOperatorReview)
            {
                return HostAgentResponse.Rejected(
                    "A previous update needs operator review before another can start. Run Deploy-ITAdmin.ps1 on this host.",
                    request.CorrelationId);
            }

            var access = await gitClient.DiagnoseAccessAsync(cancellationToken);
            if (!access.IsAccessible)
            {
                return new HostAgentResponse
                {
                    Status = HostAgentResponseStatus.Failed,
                    Message = access.Message,
                    CorrelationId = request.CorrelationId,
                    RepositoryStatus = access.Status,
                };
            }

            var availability = await gitClient.GetAvailabilityAsync(cancellationToken);
            var operationId = Guid.NewGuid().ToString("N");
            WriteOperation(new UpdateOperationRecord
            {
                OperationId = operationId,
                Phase = HostAgentUpdatePhase.Pulling,
                TargetCommit = availability.LatestCommit,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Message = "Handing the update to the Update Coordinator.",
            });

            var handoff = await executor.ApplyUpdateAsync(operationId, cancellationToken);
            if (!handoff.Succeeded)
            {
                WriteOperation(new UpdateOperationRecord
                {
                    OperationId = operationId,
                    Phase = HostAgentUpdatePhase.Failed,
                    TargetCommit = availability.LatestCommit,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = handoff.Message,
                });
                return HostAgentResponse.Failed(handoff.Message, request.CorrelationId);
            }

            return new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Accepted,
                Message = availability.UpToDate
                    ? "Redeploying the current branch tip."
                    : $"Updating to {availability.LatestCommit}: {availability.LatestSubject}",
                CorrelationId = request.CorrelationId,
                Update = ToStatus(ReadOperation()),
            };
        }
        finally
        {
            _updateGate.Release();
        }
    }

    public Task<HostAgentResponse> GetUpdateStatusAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Update status read.",
            CorrelationId = request.CorrelationId,
            Update = ToStatus(ReadOperation()),
        });
    }

    public async Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var result = await executor.RecycleAppPoolAsync(settings.AppPoolName, cancellationToken);
        return result.Succeeded
            ? HostAgentResponse.Ok(result.Message, request.CorrelationId)
            : HostAgentResponse.Failed(result.Message, request.CorrelationId);
    }

    public Task<HostAgentResponse> GetHttpsStatusAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "HTTPS status read.",
            CorrelationId = request.CorrelationId,
            Https = ReadHttpsStatus(),
        });
    }

    public async Task<HostAgentResponse> ConfigureHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return HostAgentResponse.Failed("HTTPS configuration is available only on Windows.", request.CorrelationId);
        }

        var port = request.HttpsPort is > 0 and <= 65535 ? request.HttpsPort.Value : 443;

        // The certificate import is delegated to Deploy-ITAdmin.ps1 (Import-PfxCertificate), not
        // done in-process: X509CertificateLoader with MachineKeySet fails with "Access is denied"
        // on hardened hosts even as LocalSystem, while the OS import the operator would run by hand
        // works. The PFX lands in a short-lived file under the ACL'd state directory (SYSTEM and
        // Administrators only, never the app pool), and is shredded in the finally below; the
        // password is handed to the child process through its environment, never the command line.
        Directory.CreateDirectory(settings.StateRoot);
        var pfxPath = Path.Combine(settings.StateRoot, $"https-import-{Guid.NewGuid():N}.pfx");
        var pfxBytes = Convert.FromBase64String(request.PfxBase64!);

        ReleaseUpdateResult result;
        try
        {
            await File.WriteAllBytesAsync(pfxPath, pfxBytes, cancellationToken);
            RestrictToSystemAndAdministrators(pfxPath);

            var arguments = new List<string>
            {
                "-ConfigureHttps",
                "-PfxImportPath", pfxPath,
                "-HttpsPort", port.ToString(CultureInfo.InvariantCulture),
            };
            if (request.RedirectHttpToHttps == true)
            {
                arguments.Add("-RedirectHttpToHttps");
            }

            var environment = new Dictionary<string, string>
            {
                ["ITADMIN_HTTPS_PFX_PASSWORD"] = request.PfxPassword ?? string.Empty,
            };

            result = await executor.RunDeployScriptAsync(arguments, environment, cancellationToken);
        }
        finally
        {
            Array.Clear(pfxBytes);
            ShredFile(pfxPath);
        }

        if (!result.Succeeded)
        {
            return HostAgentResponse.Failed(result.Message, request.CorrelationId);
        }

        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = $"HTTPS is bound on port {port}.",
            CorrelationId = request.CorrelationId,
            Https = ReadHttpsStatus(),
        };
    }

    public async Task<HostAgentResponse> DisableHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var result = await executor.RunDeployScriptAsync(["-DisableHttps"], environment: null, cancellationToken);
        return result.Succeeded
            ? new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                Message = "HTTPS disabled; the site is HTTP-only.",
                CorrelationId = request.CorrelationId,
                Https = ReadHttpsStatus(),
            }
            : HostAgentResponse.Failed(result.Message, request.CorrelationId);
    }

    public void LogOperationFailure(HostAgentOperation operation, Exception exception) =>
        logger.LogError(exception, "{Operation} failed.", operation);

    // ------------------------------------------------------------------------------------------

    private HostAgentHttpsStatus ReadHttpsStatus()
    {
        var config = ReadAppConfigHttps();
        var status = new HostAgentHttpsStatus
        {
            Enabled = config?.Enabled ?? false,
            Port = config?.Port ?? 443,
            RedirectHttpToHttps = config?.RedirectHttpToHttps ?? false,
            CertificateThumbprint = config?.CertificateThumbprint,
        };

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(status.CertificateThumbprint))
        {
            return status;
        }

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var match = store.Certificates.Find(
                X509FindType.FindByThumbprint, status.CertificateThumbprint, validOnly: false);
            if (match.Count > 0)
            {
                status = status with
                {
                    CertificateSubject = match[0].Subject,
                    CertificateNotAfterUtc = match[0].NotAfter.ToUniversalTime(),
                };
            }
        }
        catch (CryptographicException)
        {
            // The thumbprint is recorded but the cert is gone from the store; report what we have.
        }

        return status;
    }

    private AppConfigHttps? ReadAppConfigHttps()
    {
        try
        {
            var path = Path.Combine(settings.ConfigRoot, "app.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("web", out var web)
                || !web.TryGetProperty("https", out var https))
            {
                return null;
            }

            return new AppConfigHttps(
                https.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True,
                https.TryGetProperty("port", out var port) && port.TryGetInt32(out var portValue) ? portValue : 443,
                https.TryGetProperty("redirectHttpToHttps", out var redirect) && redirect.ValueKind == JsonValueKind.True,
                https.TryGetProperty("certificateThumbprint", out var thumbprint) ? thumbprint.GetString() : null);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Locks a file down to <c>SYSTEM</c> and <c>Administrators</c> only, with inheritance off, so
    /// the transient PFX is never readable by the application pool identity or anyone else.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private void RestrictToSystemAndAdministrators(string path)
    {
        try
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            logger.LogWarning(exception, "Could not tighten the ACL on the transient PFX file; it will still be shredded.");
        }
    }

    /// <summary>Overwrites a file with zeros and deletes it; best effort.</summary>
    private void ShredFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var length = new FileInfo(path).Length;
            if (length > 0)
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    stream.Write(new byte[length]);
                    stream.Flush();
                }
            }

            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The transient PFX file {Path} could not be removed.", path);
        }
    }

    private sealed record AppConfigHttps(bool Enabled, int Port, bool RedirectHttpToHttps, string? CertificateThumbprint);

    private static bool IsTerminal(HostAgentUpdatePhase phase) =>
        phase is HostAgentUpdatePhase.Idle or HostAgentUpdatePhase.Completed
            or HostAgentUpdatePhase.Failed or HostAgentUpdatePhase.RequiresOperatorReview;

    private static HostAgentUpdateStatus ToStatus(UpdateOperationRecord? record)
    {
        if (record is null)
        {
            return new HostAgentUpdateStatus { Phase = HostAgentUpdatePhase.Idle, Message = "No update has been requested." };
        }

        return new HostAgentUpdateStatus
        {
            OperationId = record.OperationId,
            Phase = record.Phase,
            TargetCommit = record.TargetCommit,
            StartedAtUtc = record.StartedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            Message = record.Message,
        };
    }

    private UpdateOperationRecord? ReadOperation()
    {
        try
        {
            var json = AtomicStateFile.Read(settings.UpdateOperationPath);
            return json is null ? null : JsonSerializer.Deserialize<UpdateOperationRecord>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteOperation(UpdateOperationRecord record)
    {
        try
        {
            AtomicStateFile.Write(settings.UpdateOperationPath, JsonSerializer.Serialize(record, JsonOptions));
        }
        catch (Exception exception)
        {
            // The whole update pipeline reads this file to know what to do next, so a lost write is
            // not a warning-level event - it strands the operation.
            logger.LogError(exception, "Could not persist the update operation record to {Path}.",
                settings.UpdateOperationPath);
        }
    }

    private DeployStateRecord? ReadDeployState()
    {
        try
        {
            var json = AtomicStateFile.Read(settings.DeployStatePath);
            return json is null ? null : JsonSerializer.Deserialize<DeployStateRecord>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private async Task<bool> IsLocallyHealthyAsync(CancellationToken cancellationToken)
    {
        // The site may answer only on a specific host header, so localhost / the machine name would
        // 404. Send the request to the loopback address but carry the configured host header.
        var (port, hostHeader) = ReadAppConfigWeb();

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/health");
            if (!string.IsNullOrWhiteSpace(hostHeader))
            {
                request.Headers.Host = hostHeader;
            }

            var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private (int Port, string? HostHeader) ReadAppConfigWeb()
    {
        try
        {
            var path = Path.Combine(settings.ConfigRoot, "app.json");
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("web", out var web))
                {
                    var port = web.TryGetProperty("httpPort", out var p) && p.TryGetInt32(out var portValue) ? portValue : 80;
                    var hostHeader = web.TryGetProperty("httpHostHeader", out var h) && h.ValueKind == JsonValueKind.String
                        ? h.GetString()
                        : null;
                    return (port, hostHeader);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // fall through to defaults
        }

        return (80, null);
    }
}

/// <summary>
/// Applies an update to this machine and performs narrow app-pool control. An interface so the
/// agent's orchestration is testable off Windows, and so the single real implementation is the only
/// place that knows the Update Coordinator and IIS are involved.
/// </summary>
public interface IHostDeploymentExecutor
{
    Task<ReleaseUpdateResult> ApplyUpdateAsync(string operationId, CancellationToken cancellationToken);

    Task<ReleaseUpdateResult> RecycleAppPoolAsync(string appPoolName, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the checked-out <c>Deploy-ITAdmin.ps1</c> synchronously with the given extra arguments
    /// (e.g. <c>-ConfigureHttps -PfxImportPath ...</c>). Used for changes that never replace the
    /// Host Agent binary, so no Update Coordinator handoff is needed. <paramref name="environment"/>
    /// entries are added to the child process only - the way a secret (a PFX password) is passed
    /// without putting it on the command line.
    /// </summary>
    Task<ReleaseUpdateResult> RunDeployScriptAsync(
        IReadOnlyList<string> extraArguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken);
}

public sealed record ReleaseUpdateResult(bool Succeeded, string Message);

/// <summary>What the agent and the Update Coordinator both read and write to track one update.</summary>
public sealed record UpdateOperationRecord
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public HostAgentUpdatePhase Phase { get; init; }

    [JsonPropertyName("targetCommit")]
    public string? TargetCommit { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset? StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>The subset of <c>deploy.json</c> the agent reads. Written by <c>Deploy-ITAdmin.ps1</c>.</summary>
public sealed record DeployStateRecord
{
    [JsonPropertyName("activeSha")]
    public string? ActiveSha { get; init; }

    [JsonPropertyName("previousSha")]
    public string? PreviousSha { get; init; }

    [JsonPropertyName("activeBuiltAtUtc")]
    public string? ActiveBuiltAtUtc { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }

    [JsonPropertyName("lastMigration")]
    public string? LastMigration { get; init; }
}
