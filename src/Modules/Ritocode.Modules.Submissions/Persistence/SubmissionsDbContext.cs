using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Submissions.Persistence;

public sealed class SubmissionsDbContext(DbContextOptions<SubmissionsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "submissions";

    public override string Schema => SchemaName;

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<SubmissionReport> SubmissionReports => Set<SubmissionReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new SubmissionConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionReportConfiguration());
    }
}
