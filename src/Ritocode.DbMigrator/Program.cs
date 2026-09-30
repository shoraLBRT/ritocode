using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ritocode.Api.Setup;
using Ritocode.DbMigrator;
using Ritocode.Shared.Modules;

var builder = Host.CreateApplicationBuilder(args);

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
