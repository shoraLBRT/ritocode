namespace Ritocode.DbMigrator;

/// <summary>What the tool was asked to do. Deliberately tiny — this is a deploy step, not a CLI.</summary>
internal enum MigratorCommand
{
    Apply,
    Status,
    Usage,
}

internal static class MigratorCommandParser
{
    public static MigratorCommand Parse(string[] args) => args switch
    {
        [] or ["apply"] => MigratorCommand.Apply,
        ["status"] => MigratorCommand.Status,
        _ => MigratorCommand.Usage,
    };

    public static int PrintUsage()
    {
        Console.WriteLine("""
            Ritocode database migrator.

              dotnet run --project src/Ritocode.DbMigrator            apply pending migrations
              dotnet run --project src/Ritocode.DbMigrator -- status  report pending migrations

            The connection string comes from Database:ConnectionString, so it can be supplied by
            appsettings, user secrets, or the Database__ConnectionString environment variable.

            Exit codes: 0 success, 1 failure, 2 pending migrations (status only).
            """);

        return ExitCodes.Failure;
    }
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;

    /// <summary>Status found pending migrations. Distinct from failure so CI can branch on it.</summary>
    public const int Pending = 2;
}
