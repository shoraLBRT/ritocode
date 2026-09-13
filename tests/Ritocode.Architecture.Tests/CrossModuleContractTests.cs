using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Setup;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Executable form of the contract rules from docs/adr/0007-cross-module-contract-form.md §7.
/// <see cref="ModuleBoundaryTests"/> proves no module references another; these prove that what
/// modules share instead keeps the shape that makes the coupling visible.
/// </summary>
/// <remarks>
/// Only <c>Ritocode.Shared.Contracts</c> is inspected. <c>Ritocode.Shared.Identity</c> is not a
/// contract — <c>ICurrentUser</c> reports what authentication established and no module answers it
/// (ADR 0008 §1) — so it is deliberately outside the namespace these rules sweep.
/// </remarks>
public sealed class CrossModuleContractTests
{
    private const string ContractNamespace = "Ritocode.Shared.Contracts";
    private const string ModuleAssemblyPrefix = "Ritocode.Modules.";

    private static readonly Type[] ContractTypes =
    [
        .. typeof(IUserLookup).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace is { } ns
                           && (ns == ContractNamespace || ns.StartsWith(ContractNamespace + ".", StringComparison.Ordinal))),
    ];

    private static readonly Type[] ContractInterfaces = [.. ContractTypes.Where(type => type.IsInterface)];

    [Fact]
    public void ContractsExist_SoTheRulesBelowAreNotVacuous()
    {
        // Every assertion here is "for each contract". Moving the namespace would make all of them
        // pass over an empty set, which is the failure this one exists to catch.
        Assert.NotEmpty(ContractInterfaces);
    }

    [Fact]
    public void EveryContractType_IsAnInterfaceOrASealedRecord()
    {
        // §1 and §3: a contract is a question and the flat data that answers it. A class with
        // behaviour here would be a module's logic living in the one assembly every module sees.
        var violations = ContractTypes
            .Where(type => !type.IsInterface && !IsSealedRecord(type))
            .Select(type => type.FullName!)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Types under Ritocode.Shared.Contracts must be interfaces or sealed records. "
            + $"Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EveryContractMethod_ReturnsAValueAndTakesACancellationTokenLast()
    {
        // §2 and §4 in mechanical form. A bare Task is a command, and contracts are read-only; a
        // synchronous signature forecloses the database query behind it; a missing token makes the
        // round trip uncancellable. Property accessors are methods too, so a property fails here.
        var violations = new List<string>();

        foreach (var contract in ContractInterfaces)
        {
            var methods = contract.GetMethods()
                .Concat(contract.GetInterfaces().SelectMany(inherited => inherited.GetMethods()));

            foreach (var method in methods)
            {
                var returnsValue = method.ReturnType.IsGenericType
                                   && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);

                var parameters = method.GetParameters();
                var tokenLast = parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken);

                if (!returnsValue || !tokenLast)
                {
                    violations.Add($"{contract.Name}.{method.Name}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Contract methods must return Task<T> and take a CancellationToken as their last parameter. "
            + $"Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EveryContract_IsRegisteredOnceByTheModuleThatOwnsIt()
    {
        // §5 and §7.3. Routing a call through an interface turns a missing registration into a
        // startup failure; this turns it back into a test failure. The services inspected are the
        // host's real composition, not a list kept beside it.
        var services = ComposeHostServices();
        var violations = new List<string>();

        foreach (var contract in ContractInterfaces)
        {
            var registrations = services.Where(descriptor => descriptor.ServiceType == contract).ToArray();

            if (registrations.Length != 1)
            {
                violations.Add($"{contract.Name} is registered {registrations.Length} times");
                continue;
            }

            var registration = registrations[0];

            // A factory hides its implementation type, and a keyed registration is not what a
            // constructor parameter resolves. Both would defeat the point of reading the owner here.
            var implementation = registration.IsKeyedService
                ? null
                : registration.ImplementationType ?? registration.ImplementationInstance?.GetType();

            if (implementation is null)
            {
                violations.Add($"{contract.Name} is not registered by implementation type");
                continue;
            }

            var owner = ModuleAssemblyPrefix + OwnerSegment(contract);
            var actual = implementation.Assembly.GetName().Name;

            if (!string.Equals(actual, owner, StringComparison.Ordinal))
            {
                violations.Add($"{contract.Name} is implemented in {actual}, expected {owner}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Every contract is implemented and registered exactly once by the module its namespace names. "
            + $"Violations: {string.Join("; ", violations)}");
    }

    private static bool IsSealedRecord(Type type) =>
        type is { IsClass: true, IsSealed: true }
        // The compiler emits this clone method for every record class and for nothing else.
        && type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null;

    /// <summary>
    /// <c>Ritocode.Shared.Contracts.Users</c> names <c>Users</c>. A contract declared directly in
    /// <c>Ritocode.Shared.Contracts</c> names nobody, and so matches no module.
    /// </summary>
    private static string OwnerSegment(Type contract) =>
        contract.Namespace!.Length > ContractNamespace.Length
            ? contract.Namespace[(ContractNamespace.Length + 1)..].Split('.')[0]
            : "<no owner>";

    private static IServiceCollection ComposeHostServices()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddRitocodeApi();

        return builder.Services;
    }
}
