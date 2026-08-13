using ITAdmin.Deployment;
using ITAdmin.HostAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

// ITAdmin Host Agent: the privileged half of an ITAdmin installation.
//
// Runs as a Windows service under LocalSystem. It owns the machine operations the web application
// must never be able to perform - fetching releases with the deploy key, staging and activating
// them, and reconciling IIS bindings - and exposes them only as the fixed set of typed operations
// in ITAdmin.HostAgent.Contracts, over an ACL'd local named pipe.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine(
        "The ITAdmin Host Agent manages IIS and Windows services and runs on Windows only.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = HostAgentServiceMetadata.ServiceName);

var layout = DeploymentLayout.Default();
var settingsPath = Path.Combine(layout.ConfigRoot, HostAgentSettings.FileName);

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine(
        $"Host agent configuration not found at {settingsPath}. Run the ITAdmin bootstrap to install it.");
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
builder.Services.AddSingleton(serviceProvider => new GitReleaseClient(
    serviceProvider.GetRequiredService<HostAgentSettings>()));

builder.Services.AddSingleton<IReleaseUpdateExecutor>(serviceProvider =>
    new InstallerReleaseUpdateExecutor(
        serviceProvider.GetRequiredService<HostAgentSettings>(),
        Path.Combine(
            serviceProvider.GetRequiredService<HostAgentSettings>().ProgramFilesRoot,
            "tooling",
            "install",
            "Install-ITAdmin.ps1"),
        serviceProvider.GetRequiredService<ILogger<InstallerReleaseUpdateExecutor>>()));

builder.Services.AddSingleton<IWebBindingReconciler, IisWebBindingReconciler>();
builder.Services.AddSingleton<IHostAgentOperations, DeploymentHostAgentOperations>();
builder.Services.AddSingleton<HostAgentDispatcher>();
builder.Services.AddHostedService<HostAgentPipeServer>();

await builder.Build().RunAsync();
return 0;

/// <summary>Names shared between the agent and whatever installs it.</summary>
public static class HostAgentServiceMetadata
{
    public const string ServiceName = "ITAdminHostAgent";

    public const string DisplayName = "ITAdmin Host Agent";

    public const string Description =
        "Performs privileged ITAdmin host operations (release updates and IIS binding "
        + "reconciliation) on behalf of the ITAdmin application, over a local ACL'd named pipe.";
}
