using System.Reflection;
using Ritocode.Api.Setup;
using Ritocode.Shared.Modules;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Executable form of the module boundary rule from docs/adr/0002-modular-monolith-layout.md.
/// Documentation drifts; a failing test does not.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string ModuleAssemblyPrefix = "Ritocode.Modules.";

    private static readonly Assembly[] ModuleAssemblies =
        [.. ModuleRegistry.All.Select(m => m.GetType().Assembly).Distinct()];

    [Fact]
    public void EveryRegisteredModule_LivesInItsOwnAssembly()
    {
        Assert.Equal(ModuleRegistry.All.Count, ModuleAssemblies.Length);
    }

    [Fact]
    public void NoModule_ReferencesAnotherModule()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var ownName = assembly.GetName().Name!;

            var offending = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith(ModuleAssemblyPrefix, StringComparison.Ordinal)
                               && !string.Equals(name, ownName, StringComparison.Ordinal));

            violations.AddRange(offending.Select(name => $"{ownName} -> {name}"));
        }

        Assert.True(
            violations.Count == 0,
            "Modules must not reference each other; route cross-module contracts through Ritocode.Shared. "
            + $"Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void NoModule_ReferencesTheApiHost()
    {
        var violations = ModuleAssemblies
            .Where(a => a.GetReferencedAssemblies().Any(r => r.Name == "Ritocode.Api"))
            .Select(a => a.GetName().Name!)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Modules must not depend on the composition root. Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EveryModule_DeclaresANameAndRoutePrefix()
    {
        foreach (var module in ModuleRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(module.Name), $"{module.GetType().Name} has no Name.");
            Assert.False(string.IsNullOrWhiteSpace(module.RoutePrefix), $"{module.Name} has no RoutePrefix.");
            Assert.Equal(module.RoutePrefix.ToLowerInvariant(), module.RoutePrefix);
        }
    }

    [Fact]
    public void ModuleRegistry_ContainsEveryModuleTypeInTheSolution()
    {
        // A module project added but never registered is dead weight; a module registered twice is a bug.
        var registeredTypes = ModuleRegistry.All.Select(m => m.GetType()).ToHashSet();

        var declaredTypes = ModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in declaredTypes)
        {
            Assert.Contains(type, registeredTypes);
        }
    }
}
