using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ITAdmin.UnitTests.Fakes;

public sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "ITAdmin.Api";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
