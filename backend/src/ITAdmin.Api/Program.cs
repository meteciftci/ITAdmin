using ITAdmin.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

try
{
    // Deployment modes run before any web host is built so the installer can provision, migrate,
    // or inspect the schema using the application's own configuration, with no toolchain on the
    // server.
    if (DatabaseMigrationRunner.IsMigrateRequested(args))
    {
        Environment.ExitCode = await DatabaseMigrationRunner.RunAsync(args, Console.Out, Console.Error);
        return;
    }

    if (DatabaseMigrationRunner.IsStatusRequested(args))
    {
        Environment.ExitCode = await DatabaseMigrationRunner.RunStatusAsync(args, Console.Out, Console.Error);
        return;
    }

    if (DatabaseProvisionRunner.IsRequested(args))
    {
        Environment.ExitCode = await DatabaseProvisionRunner.RunAsync(args, Console.Out, Console.Error);
        return;
    }

    if (DirectoryBootstrapRunner.IsRequested(args))
    {
        Environment.ExitCode = await DirectoryBootstrapRunner.RunAsync(args, Console.Out, Console.Error);
        return;
    }

    Log.Information("Starting ITAdmin API");
    Program.CreateWebApplication(args).Run();
}
catch (HostAbortedException) when (EF.IsDesignTime)
{
    // Expected during EF Core design-time operations (dotnet ef).
}
catch (HostAbortedException exception)
{
    Console.Error.WriteLine(exception);
    Log.Fatal(exception, "ITAdmin API host was aborted");
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Log.Fatal(exception, "ITAdmin API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
