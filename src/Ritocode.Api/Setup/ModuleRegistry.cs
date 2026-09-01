using Ritocode.Modules.Auth;
using Ritocode.Modules.Evaluations;
using Ritocode.Modules.Problems;
using Ritocode.Modules.Progress;
using Ritocode.Modules.Submissions;
using Ritocode.Modules.Users;
using Ritocode.Modules.Workspaces;
using Ritocode.Shared.Modules;

namespace Ritocode.Api.Setup;

/// <summary>
/// The explicit list of modules composing the backend. Assembly scanning is deliberately avoided
/// so that adding a module is a visible, reviewable edit in exactly one file.
/// </summary>
public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new AuthModule(),
        new UsersModule(),
        new ProblemsModule(),
        new WorkspacesModule(),
        new SubmissionsModule(),
        new EvaluationsModule(),
        new ProgressModule(),
    ];
}
