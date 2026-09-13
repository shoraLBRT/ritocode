using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Modules.Workspaces.Persistence;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Executable form of the ownership row in docs/adr/0005-vertical-slice-before-breadth.md's forbidden
/// list: a workspace or a submission is never served without checking it belongs to the caller.
/// </summary>
/// <remarks>
/// <para>
/// The check is kept where it cannot be forgotten by keeping it out of the endpoints: a user's rows are
/// reached only through a lookup that puts the owner inside the query, and another user's row is then
/// the same absent row as a missing one — a 404, never a 403 (ADR 0003). This test fails when code in
/// either module reaches those rows anywhere else. An EF global query filter was the alternative, and
/// was not chosen: it puts the current user inside a <see cref="DbContext"/>, which the queue worker of
/// #15 — serving no user at all — and every test that writes another user's row would have to switch
/// off, and a switch that exists is the thing that gets flipped.
/// </para>
/// <para>
/// "Reaching" a row is naming a member of a context whose result or type argument is one of its
/// entities: the set property, <c>Set&lt;T&gt;</c>, <c>Find&lt;T&gt;</c>, <c>Add&lt;T&gt;</c>,
/// <c>Entry&lt;T&gt;</c>, and <c>Database.SqlQuery&lt;T&gt;</c>. The non-generic <c>Find(Type, …)</c> and
/// SQL sent as a string through <c>ExecuteSql</c> are not seen; neither is a way anyone reads a row by
/// accident.
/// </para>
/// </remarks>
public sealed class OwnershipRuleTests
{
    /// <summary>The contexts whose rows belong to a user. Every entity either one maps is guarded.</summary>
    private static readonly Type[] OwnedContexts = [typeof(WorkspacesDbContext), typeof(SubmissionsDbContext)];

    /// <summary>
    /// Where a user's rows may be reached, and why. A new entry is a claim a reviewer reads; the reason
    /// is the part that has to be true.
    /// </summary>
    private static readonly Allowance[] Allowances =
    [
        new(
            "Ritocode.Modules.Workspaces.Persistence.OwnedWorkspaces",
            Method: null,
            "Every lookup there takes the owner and puts it inside the query."),
        new(
            typeof(WorkspaceLifecycle).FullName!,
            nameof(WorkspaceLifecycle.OpenAsync),
            "Finds the caller's workspace by owner and version, and adds the row it creates for that owner."),
    ];

    private static readonly HashSet<Type> GuardedEntities = [.. OwnedContexts.SelectMany(EntitiesMappedBy)];

    [Fact]
    public void TheRule_GuardsWorkspacesAndSubmissions_SoItIsNotVacuous()
    {
        // Every assertion below is "for each guarded entity". A context that mapped nothing, or a model
        // that failed to build into an empty one, would make them pass over nothing.
        Assert.Contains(typeof(Workspace), GuardedEntities);
        Assert.Contains(typeof(Submission), GuardedEntities);
        Assert.Contains(typeof(SubmissionReport), GuardedEntities);
    }

    [Fact]
    public void AUsersRows_AreReachedOnlyWhereTheOwnerIsInTheQuery()
    {
        var violations = ModuleAccesses()
            .Where(access => !Allowances.Any(allowance => allowance.Permits(access)))
            .Select(access => access.ToString())
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "A workspace or submission row is reached outside a lookup that puts the owner in the query. "
            + "Go through OwnedWorkspaces (or the Submissions equivalent), or add an allowance here that says why "
            + $"the owner is not needed. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void EveryAllowance_StillReachesAUsersRows()
    {
        // An allowance nothing uses is either left behind by a rename — and would silently permit a new
        // method of the same name — or proof the reader has stopped seeing the code it was written for.
        var accesses = ModuleAccesses().ToArray();

        var unused = Allowances
            .Where(allowance => !accesses.Any(allowance.Permits))
            .Select(allowance => allowance.ToString())
            .ToArray();

        Assert.True(unused.Length == 0, $"Allowances that permit nothing: {string.Join("; ", unused)}");
    }

    [Fact]
    public void TheReader_SeesEveryShapeOfReachingAUsersRows()
    {
        var expected = typeof(UnguardedReads)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal);

        var seen = AccessesIn(typeof(UnguardedReads).Assembly.GetTypes().Where(type => IsWithin(typeof(UnguardedReads), type)))
            .Select(access => access.Method)
            .Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, seen);
    }

    private static IEnumerable<Access> ModuleAccesses() =>
        AccessesIn(OwnedContexts.Select(context => context.Assembly).Distinct().SelectMany(assembly => assembly.GetTypes()));

    private static IEnumerable<Access> AccessesIn(IEnumerable<Type> types) =>
        types
            // A context declares its own sets; that is the one place naming them is the definition.
            .Where(type => !typeof(DbContext).IsAssignableFrom(type))
            .SelectMany(MethodBodyReferences.BodiesIn)
            .SelectMany(body => MethodBodyReferences.Of(body)
                .Where(ReachesAGuardedEntity)
                .Select(member =>
                {
                    var (type, method) = MethodBodyReferences.SourceOf(body);
                    return new Access(type, method, member);
                }));

    private static bool ReachesAGuardedEntity(MethodBase member)
    {
        var typeArguments = member.IsGenericMethod ? member.GetGenericArguments() : [];
        var result = member is MethodInfo method ? method.ReturnType : null;

        if (!typeArguments.Any(Mentions) && (result is null || !Mentions(result)))
        {
            return false;
        }

        return typeof(DbContext).IsAssignableFrom(member.DeclaringType)
               || member.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(DatabaseFacade);
    }

    private static bool Mentions(Type type) =>
        GuardedEntities.Contains(type)
        || (type.HasElementType && Mentions(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(Mentions));

    private static bool IsWithin(Type outer, Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current == outer)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What the context maps, read from the model EF builds rather than from its set properties, so an
    /// entity configured without a set is guarded too. Building a model opens no connection.
    /// </summary>
    private static IEnumerable<Type> EntitiesMappedBy(Type contextType)
    {
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;

        using var context = (DbContext)Activator.CreateInstance(contextType, builder.UseNpgsql().Options)!;

        return [.. context.Model.GetEntityTypes().Select(entity => entity.ClrType)];
    }

    private sealed record Access(Type Type, string Method, MethodBase Member)
    {
        public override string ToString() => $"{Type.FullName}.{Method} -> {Member.DeclaringType?.Name}.{Member.Name}";
    }

    private sealed record Allowance(string Type, string? Method, string Reason)
    {
        public bool Permits(Access access) =>
            string.Equals(access.Type.FullName, Type, StringComparison.Ordinal)
            && (Method is null || string.Equals(access.Method, Method, StringComparison.Ordinal));

        public override string ToString() => Method is null ? Type : $"{Type}.{Method}";
    }
}
