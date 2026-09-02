namespace Ritocode.Shared.Persistence;

/// <summary>
/// Records that a module context was registered, so tooling can enumerate every schema without
/// keeping a second list that would drift from the modules themselves.
/// </summary>
/// <param name="ContextType">The registered <see cref="ModuleDbContext"/> type.</param>
/// <param name="Schema">The PostgreSQL schema that context owns.</param>
public sealed record ModuleDbContextRegistration(Type ContextType, string Schema);
