using ITAdmin.HostAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

// ITAdmin Host Agent: the privileged half of an ITAdmin installation.
//
// Runs as a Windows service under LocalSystem. It owns the machine operations the web application
// must never be able to perform - applying an update by running the checked-out deployment script,
// and recycling the application pool - and exposes them only as the fixed set of typed operations
// in ITAdmin.HostAgent.Contracts, over an ACL'd local named pipe.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine(
        "The ITAdmin Host Agent manages IIS and Windows services and runs on Windows only.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = HostAgentServiceMetadata.ServiceName);

// Deploy-ITAdmin.ps1 records the ProgramData root in the registry before the service is
// registered. Reading the same machine value as the Update Coordinator keeps a custom -DataRoot
// coherent across both LocalSystem processes instead of silently falling back to the default in
// one of them.
const string DefaultProgramDataRoot = @"C:\ProgramData\ITAdmin";

#pragma warning disable CA1416 // Reached only after the Windows guard above.
var programDataRoot = Registry.GetValue(
    @"HKEY_LOCAL_MACHINE\SOFTWARE\ITAdmin",
    "ProgramDataRoot",
    DefaultProgramDataRoot) as string ?? DefaultProgramDataRoot;
#pragma warning restore CA1416

var settingsPath = Path.Combine(programDataRoot, "config", HostAgentSettings.FileName);

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine(
        $"Host agent configuration not found at {settingsPath}. Run Deploy-ITAdmin.ps1 to install it.");
    return 3;
}

var settings = HostAgentSettings.FromJson(await File.ReadAllTextAsync(settingsPath));
if (settings is null)
{
    Console.Error.WriteLine($"Host agent configuration at {settingsPath} is not valid JSON.");
    return 3;
}

var settingsProblems = settings.Validate();
if (settingsProblems.Count > 0)
{
    foreach (var problem in settingsProblems)
    {
        Console.Error.WriteLine($"Host agent configuration is invalid: {problem}");
    }

    return 3;
}

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(new HostAgentAuthorization(settings.AppPoolName));
builder.Services.AddSingleton(serviceProvider => new GitSourceClient(
    serviceProvider.GetRequiredService<HostAgentSettings>()));

#pragma warning disable CA1416 // Reached only after the Windows guard above.
builder.Services.AddSingleton<IHostDeploymentExecutor, WindowsHostDeploymentExecutor>();
#pragma warning restore CA1416

builder.Services.AddSingleton<IHostAgentOperations, DeploymentHostAgentOperations>();
builder.Services.AddSingleton<HostAgentDispatcher>();
builder.Services.AddHostedService<HostAgentPipeServer>();

var app = builder.Build();
app.Services.GetRequiredService<IHostAgentOperations>().ReconcileInterruptedOperation();
await app.RunAsync();
return 0;

/// <summary>Names shared between the agent and whatever installs it.</summary>
public static class HostAgentServiceMetadata
{
    public const string ServiceName = "ITAdminHostAgent";

    public const string DisplayName = "ITAdmin Host Agent";

    public const string Description =
        "Applies updates by running the checked-out ITAdmin deployment script, and performs narrow "
        + "IIS operations, on behalf of the ITAdmin application over a local ACL'd named pipe.";
}
