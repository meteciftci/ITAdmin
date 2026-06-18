using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

try
{
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
