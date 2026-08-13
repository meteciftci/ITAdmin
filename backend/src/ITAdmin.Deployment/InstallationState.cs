using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Deployment;

/// <summary>
/// Where the machine is in the install lifecycle. The installer decides what to do next from this
/// phase plus the recorded versions — never from heuristics like "does the IIS folder exist",
/// which cannot distinguish a healthy install from one that died halfway through activation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstallationPhase
{
    /// <summary>No usable state on disk: a fresh machine.</summary>
    NotInstalled = 0,

    /// <summary>Payload is being written into the release directory.</summary>
    Staging = 1,

    /// <summary>Payload is on disk and integrity-verified, but not yet serving.</summary>
    Staged = 2,

    /// <summary>Machine configuration (filesystem, ACLs, IIS, config, secrets) is being applied.</summary>
    Configuring = 3,

    /// <summary>Database schema migration is in flight. See <see cref="InstallationState.MigrationInFlight"/>.</summary>
    Migrating = 4,

    /// <summary>IIS is being pointed at the staged release.</summary>
    Activating = 5,

    /// <summary>Activated and health-verified. The only phase that means "this machine is serving".</summary>
    Installed = 6,

    /// <summary>A step failed. Never silently upgraded to Installed; the installer must be re-run.</summary>
    Failed = 7,

    /// <summary>IIS features and/or the Hosting Bundle are being installed.</summary>
    ProvisioningPrerequisites = 8,

    /// <summary>
    /// A prerequisite install asked for a reboot. The installer must not continue blindly; the
    /// operator reboots and re-runs, and the next invocation resumes from detection.
    /// </summary>
    AwaitingReboot = 9,
}

/// <summary>
/// Persistent installation state for one machine, stored under ProgramData.
///
/// <para>
/// This file contains no secrets. It records identity, lifecycle position, and the last error —
/// enough for the installer to resume, repair, or refuse — while database passwords, the JWT key,
/// and the setup key live in the separately-ACL'd secret store. That split is deliberate: this
/// file is meant to be readable by an operator diagnosing a failed rollout.
/// </para>
/// </summary>
public sealed record InstallationState
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "installation.json";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("product")]
    public string Product { get; init; } = ReleaseManifest.ProductName;

    [JsonPropertyName("phase")]
    public InstallationPhase Phase { get; init; } = InstallationPhase.NotInstalled;

    /// <summary>Version IIS is currently serving. Null until the first successful activation.</summary>
    [JsonPropertyName("activeVersion")]
    public string? ActiveVersion { get; init; }

    /// <summary>Version staged on disk and awaiting activation.</summary>
    [JsonPropertyName("stagedVersion")]
    public string? StagedVersion { get; init; }

    /// <summary>Version served before the current one; the rollback target once updates exist.</summary>
    [JsonPropertyName("previousVersion")]
    public string? PreviousVersion { get; init; }

    /// <summary>Newest EF migration known to have been applied to the configured database.</summary>
    [JsonPropertyName("lastMigrationApplied")]
    public string? LastMigrationApplied { get; init; }

    /// <summary>
    /// True between starting and finishing a migration. If it is still true on a later run the
    /// previous attempt died mid-migration, and the database may be partially migrated — a state
    /// the installer must surface rather than quietly retry.
    /// </summary>
    [JsonPropertyName("migrationInFlight")]
    public bool MigrationInFlight { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [JsonPropertyName("lastError")]
    public InstallationError? LastError { get; init; }

    /// <summary>
    /// The deployment operation currently in flight, if any.
    ///
    /// <para>
    /// This lives in the existing state file rather than a parallel store on purpose. The Host
    /// Agent used to hold update progress only in memory, which meant a service restart part-way
    /// through an update left a machine that could not be classified: the release directory might
    /// be half-staged, the schema might be half-migrated, and nothing on disk said so. Recording
    /// the operation next to the phase it is driving keeps one state machine and makes
    /// "interrupted" a state the next start can actually recognise.
    /// </para>
    /// </summary>
    [JsonPropertyName("currentOperation")]
    public DeploymentOperation? CurrentOperation { get; init; }

    /// <summary>
    /// Whether an ITAdmin installer run turned IIS on for the first time on this machine.
    ///
    /// <para>
    /// Recorded because the safe response to "something already owns port 80" depends entirely on
    /// it. If we provisioned IIS, the Default Web Site is a pristine artifact of that provisioning
    /// and may be stood down; if IIS pre-existed, every site on it is operator-owned - including one
    /// named "Default Web Site" that may be quietly serving something. Deciding from a site name
    /// would be guessing at exactly the wrong moment, so the history is written down instead.
    /// </para>
    /// </summary>
    [JsonPropertyName("iisProvisionedByInstaller")]
    public bool IisProvisionedByInstaller { get; init; }

    /// <summary>
    /// What first-install success actually means. Recorded so an operator - and the next installer
    /// run - can tell a machine that merely serves HTTP from one somebody can log into.
    /// </summary>
    [JsonPropertyName("readiness")]
    public InstallationReadiness Readiness { get; init; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Reads state. Unreadable or malformed state deliberately returns null rather than throwing,
    /// so the installer treats it as "unknown" and refuses to act on a guess.
    /// </summary>
    public static InstallationState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstallationState>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>State for a machine with nothing installed yet.</summary>
    public static InstallationState Fresh(DateTimeOffset now) =>
        new() { Phase = InstallationPhase.NotInstalled, UpdatedAtUtc = now };

    /// <summary>
    /// True when a previous run recorded an operation it never finished. The caller must resolve
    /// this before starting new work - silently starting a second deployment over the wreckage of
    /// the first is how a half-migrated schema becomes an unrecoverable one.
    /// </summary>
    public bool HasInterruptedOperation =>
        CurrentOperation is not null && !CurrentOperation.IsTerminal;

    /// <summary>
    /// Whether this machine is genuinely usable: serving, and with a directory-backed administrator
    /// who can log in. A process answering HTTP 200 is not the same thing.
    /// </summary>
    public bool IsUsable =>
        Phase is InstallationPhase.Installed
        && !MigrationInFlight
        && Readiness.IsComplete;

    /// <summary>
    /// Classifies what a run of the installer is being asked to do, given this state and the
    /// version of the artifact presented.
    /// </summary>
    public InstallationIntent ClassifyIntent(ReleaseVersion candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (MigrationInFlight)
        {
            return InstallationIntent.RecoverInterruptedMigration;
        }

        if (Phase is InstallationPhase.AwaitingReboot)
        {
            return InstallationIntent.ResumeAfterReboot;
        }

        if (Phase is InstallationPhase.Failed
            or InstallationPhase.Staging
            or InstallationPhase.Configuring
            or InstallationPhase.Activating
            or InstallationPhase.ProvisioningPrerequisites)
        {
            return InstallationIntent.ResumeFailedInstall;
        }

        if (Phase is InstallationPhase.NotInstalled || ActiveVersion is null)
        {
            return InstallationIntent.FreshInstall;
        }

        if (!ReleaseVersion.TryParse(ActiveVersion, out var active))
        {
            return InstallationIntent.ResumeFailedInstall;
        }

        var comparison = candidate.CompareTo(active);
        return comparison switch
        {
            0 => InstallationIntent.SameVersionRepair,
            > 0 => InstallationIntent.Upgrade,
            _ => InstallationIntent.Downgrade,
        };
    }
}

/// <summary>What a given installer run means for this machine.</summary>
public enum InstallationIntent
{
    FreshInstall,
    SameVersionRepair,
    Upgrade,

    /// <summary>Installing an older release than the active one; requires explicit operator intent.</summary>
    Downgrade,

    /// <summary>A previous run failed part-way; the installer must re-establish a known state.</summary>
    ResumeFailedInstall,

    /// <summary>A previous run died during migration; the database may be partially migrated.</summary>
    RecoverInterruptedMigration,

    /// <summary>
    /// A previous run stopped because Windows required a reboot after prerequisite changes.
    /// The next run re-detects prerequisites before any further install work.
    /// </summary>
    ResumeAfterReboot,
}

/// <summary>
/// The last failure, in operator-readable form. Carries a step name and message only — never a
/// connection string, credential, or raw exception dump, because this file is not secret-bearing.
/// </summary>
public sealed record InstallationError
{
    [JsonPropertyName("step")]
    public string Step { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("occurredAtUtc")]
    public DateTimeOffset OccurredAtUtc { get; init; }
}

/// <summary>
/// A deployment operation, durable across a service restart.
///
/// <para>
/// Deliberately small. This is not a job scheduler: it records which single operation this machine
/// is in the middle of, so that a restart can classify it rather than guess. Anything richer would
/// be a second state machine competing with <see cref="InstallationState.Phase"/>.
/// </para>
/// </summary>
public sealed record DeploymentOperation
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public DeploymentOperationKind Kind { get; init; }

    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    [JsonPropertyName("stage")]
    public DeploymentOperationStage Stage { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset StartedAtUtc { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Operator-facing message. Never carries a secret, a path, or a repository URL.</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    public bool IsTerminal => Stage is DeploymentOperationStage.Completed or DeploymentOperationStage.Failed;

    public static DeploymentOperation Start(
        string id,
        DeploymentOperationKind kind,
        string? targetVersion,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            Kind = kind,
            TargetVersion = targetVersion,
            Stage = DeploymentOperationStage.Resolving,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            Message = "Operation accepted.",
        };

    public DeploymentOperation Advance(DeploymentOperationStage stage, string message, DateTimeOffset now) =>
        this with { Stage = stage, Message = message, UpdatedAtUtc = now };

    /// <summary>
    /// How a restart should classify this operation. The distinction that matters is whether the
    /// interruption could have left the database or the live site partially changed: those need a
    /// human before anything else happens, while an interrupted fetch is simply discardable.
    /// </summary>
    public InterruptedOperationDisposition Classify()
    {
        if (IsTerminal)
        {
            return InterruptedOperationDisposition.Complete;
        }

        return Stage switch
        {
            // Nothing on this machine changed yet; the staged copy is a temporary directory.
            DeploymentOperationStage.Resolving
                or DeploymentOperationStage.Fetching
                or DeploymentOperationStage.Verifying => InterruptedOperationDisposition.SafeToDiscard,

            // A release directory may be half-written, but the live site is untouched.
            DeploymentOperationStage.Staging => InterruptedOperationDisposition.RetryFromStart,

            // The schema or the live site may be partially changed. Never resumed automatically.
            _ => InterruptedOperationDisposition.RequiresOperatorReview,
        };
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeploymentOperationKind
{
    FirstInstall = 0,
    Repair = 1,
    Update = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeploymentOperationStage
{
    Resolving = 0,
    Fetching = 1,
    Verifying = 2,
    Staging = 3,
    Migrating = 4,
    Activating = 5,
    Completed = 6,
    Failed = 7,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterruptedOperationDisposition
{
    /// <summary>Not interrupted.</summary>
    Complete = 0,

    /// <summary>Nothing durable changed; the operation can simply be forgotten.</summary>
    SafeToDiscard = 1,

    /// <summary>Durable but self-correcting state; re-running from the start is safe.</summary>
    RetryFromStart = 2,

    /// <summary>The schema or the live site may be partially changed. A human must look first.</summary>
    RequiresOperatorReview = 3,
}

/// <summary>
/// The four independent things that must all be true before an ITAdmin installation is worth
/// calling successful.
///
/// <para>
/// They are tracked separately because they fail separately and are fixed separately. An install
/// where IIS serves but the directory bind is wrong is a completely different problem from one
/// where the directory is fine but migrations failed - and neither is "installed".
/// </para>
/// </summary>
public sealed record InstallationReadiness
{
    /// <summary>The site answers its health endpoint.</summary>
    [JsonPropertyName("processHealthy")]
    public bool ProcessHealthy { get; init; }

    /// <summary>The application reports first-run setup as complete.</summary>
    [JsonPropertyName("setupCompleted")]
    public bool SetupCompleted { get; init; }

    /// <summary>A Primary Directory is configured and its bind was validated.</summary>
    [JsonPropertyName("directoryUsable")]
    public bool DirectoryUsable { get; init; }

    /// <summary>A directory-backed initial administrator exists.</summary>
    [JsonPropertyName("administratorBootstrapped")]
    public bool AdministratorBootstrapped { get; init; }

    public bool IsComplete =>
        ProcessHealthy && SetupCompleted && DirectoryUsable && AdministratorBootstrapped;

    /// <summary>What is still missing, in operator language.</summary>
    public IReadOnlyList<string> Describe()
    {
        var missing = new List<string>();

        if (!ProcessHealthy)
        {
            missing.Add("the site is not answering its health endpoint");
        }

        if (!DirectoryUsable)
        {
            missing.Add("no Primary Directory has been validated");
        }

        if (!AdministratorBootstrapped)
        {
            missing.Add("no directory-backed administrator has been created");
        }

        if (!SetupCompleted)
        {
            missing.Add("the application still reports first-run setup as incomplete");
        }

        return missing;
    }
}
