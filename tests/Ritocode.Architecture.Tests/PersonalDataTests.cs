using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Setup;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;
using Ritocode.Shared.PersonalData;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Executable form of ADR 0021: every module that keeps a user's rows hands them over and erases them
/// through its own <see cref="IPersonalData"/>, over its own schema, and the account's own rows are
/// one module's — so a module added later with a <c>UserId</c> and no eraser fails here, before the
/// API test that walks the schemas finds its rows left behind.
/// </summary>
public sealed class PersonalDataTests
{
    [Fact]
    public void EveryModuleThatKeepsAUsersRows_RegistersOneIPersonalData_InItsOwnAssembly_AndNoOtherModuleDoes()
    {
        var violations = new List<string>();
        var keepers = 0;

        foreach (var module in ModuleRegistry.All)
        {
            var services = RegisteredBy(module);
            var keepsRows = KeepsAUsersRows(services) || module.Name == "Users";
            var holders = services.Where(descriptor => descriptor.ServiceType == typeof(IPersonalData)).ToArray();

            if (!keepsRows)
            {
                if (holders.Length != 0)
                {
                    violations.Add($"{module.Name} keeps no user's rows but registers {holders.Length} IPersonalData");
                }

                continue;
            }

            keepers++;

            if (holders.Length != 1)
            {
                violations.Add($"{module.Name} keeps a user's rows and registers {holders.Length} IPersonalData, not one");
                continue;
            }

            var implementation = holders[0].ImplementationType;

            if (implementation is null || implementation.Assembly != module.GetType().Assembly)
            {
                violations.Add($"{module.Name}'s IPersonalData is not a type of its own assembly");
                continue;
            }

            if (Describe(implementation).Module != module.Name)
            {
                violations.Add($"{module.Name}'s IPersonalData names itself {Describe(implementation).Module}");
            }
        }

        // Not vacuous: Auth, Users and Attempts keep a user's rows today.
        Assert.True(keepers >= 3, $"Only {keepers} modules were found keeping a user's rows.");
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void TheAccountsOwnRows_AreOneModulesAndTheUsersModules()
    {
        // The account is erased last so a deletion that fails part-way can be asked again; that needs
        // exactly one holder of it.
        var accounts = ModuleRegistry.All
            .SelectMany(module => RegisteredBy(module)
                .Where(descriptor => descriptor.ServiceType == typeof(IPersonalData) && descriptor.ImplementationType is not null)
                .Select(descriptor => Describe(descriptor.ImplementationType!)))
            .Where(holder => holder.HoldsTheAccount)
            .Select(holder => holder.Module)
            .ToArray();

        Assert.Equal(["Users"], accounts);
    }

    private static ServiceCollection RegisteredBy(IModule module)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        module.RegisterServices(services, new ConfigurationBuilder().Build());
        return services;
    }

    /// <summary>Whether a context the module registers maps an entity with a <c>Guid UserId</c>.</summary>
    private static bool KeepsAUsersRows(ServiceCollection services) =>
        services
            .Where(descriptor => descriptor.ServiceType == typeof(ModuleDbContextRegistration))
            .Select(descriptor => ((ModuleDbContextRegistration)descriptor.ImplementationInstance!).ContextType)
            .SelectMany(EntitiesMappedBy)
            .Any(entity => entity.GetProperty("UserId")?.PropertyType == typeof(Guid));

    private static IEnumerable<Type> EntitiesMappedBy(Type contextType)
    {
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;

        using var context = (DbContext)Activator.CreateInstance(contextType, builder.UseNpgsql().Options)!;
        return [.. context.Model.GetEntityTypes().Select(entity => entity.ClrType)];
    }

    /// <summary>
    /// The holder's <see cref="IPersonalData.Module"/> and <see cref="IPersonalData.HoldsTheAccount"/>,
    /// read without its constructor: both are constants, and the constructor wants a database.
    /// </summary>
    private static IPersonalData Describe(Type implementation) =>
        (IPersonalData)RuntimeHelpers.GetUninitializedObject(implementation);
}
