using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ritocode.Api.Setup;
using Ritocode.DbMigrator;
using Ritocode.Shared.Modules;

// Rooted where the migrator's own files are, not where it is started from: in the release image the
// API's appsettings.json sits in the working directory, one level above this one's.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

// The same module list the API host composes, so the migrator can never apply a different set
// of schemas than the host expects.
builder.Services.AddModules(builder.Configuration, ModuleRegistry.All);

using var host = builder.Build();

var runner = new MigrationRunner(
    host.Services,
    host.Services.GetRequiredService<ILogger<MigrationRunner>>());

return MigratorCommandParser.Parse(args) switch
{
    MigratorCommand.Apply => await runner.ApplyAsync(),
    MigratorCommand.Status => await runner.ReportStatusAsync(),
    _ => MigratorCommandParser.PrintUsage(),
};
