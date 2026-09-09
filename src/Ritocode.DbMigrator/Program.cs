using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ritocode.Api.Setup;
using Ritocode.DbMigrator;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Storage;

var builder = Host.CreateApplicationBuilder(args);

// Host infrastructure the modules register services against. The migrator needs none of it to
// apply a migration, but it composes the same modules the API does, and a module that offers a
// service depending on object storage cannot be composed into a host that has none. Registration
// contacts nothing and every setting has a default, so this adds no requirement to migrating.
builder.Services.AddObjectStorage(builder.Configuration);

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
