using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ritocode.Shared.Persistence;

public static class ModuleDbContextExtensions
{
    /// <summary>Name of the migration history table each module keeps inside its own schema.</summary>
    public const string MigrationsHistoryTableName = "__migrations_history";

    /// <summary>
    /// Registers a module's <see cref="ModuleDbContext"/> against the shared PostgreSQL database,
    /// scoped to <paramref name="schema"/>. Every module calls this from
    /// <c>IModule.RegisterServices</c> with its own context type.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        // Bound once regardless of how many modules call this; options validation runs at startup.
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<TContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            builder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTableName, schema);
                npgsql.CommandTimeout(options.CommandTimeoutSeconds);

                if (options.MaxRetryCount > 0)
                {
                    npgsql.EnableRetryOnFailure(options.MaxRetryCount);
                }
            });

            // The schema is read and queried by hand in psql as often as by the application,
            // so it follows PostgreSQL convention rather than C# convention.
            builder.UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
