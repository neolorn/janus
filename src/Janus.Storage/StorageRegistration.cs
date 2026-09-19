using System;
using System.Security.Cryptography;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Audit;
using Janus.Identity.Identifiers;
using Janus.Identity.Organizations;
using Janus.Identity.Preferences;
using Janus.Identity.Profiles;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authorization.Gate;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Groups;
using Janus.Storage.Authorization.Model;
using Janus.Storage.Authorization.Resources;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Storage;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>Implements CONV-DESIGN-007.</remarks>
internal static class StorageRegistration
{
    /// <summary>
    /// Registers the context, the unit of work, the connection accessor and the
    /// persistence ports.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="connectionString">
    /// The application's own credential, which holds row-level access and no schema
    /// right (OPS-MIG-003).
    /// </param>
    /// <param name="keyEncryptionKeys">
    /// The versions a subject key may be wrapped under, read from the secrets manager
    /// at startup and never from the database (OPS-SEC-001).
    /// </param>
    /// <param name="fingerprintKey">
    /// The key the searchable fingerprints are computed under, read from the same
    /// place and held outside the database (PRIV-RIGHT-005c).
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    public static IServiceCollection AddJanusStorage(
        this IServiceCollection services,
        string connectionString,
        KeyEncryptionKeys keyEncryptionKeys,
        ReadOnlyMemory<byte> fingerprintKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<JanusDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                JanusDbContext.MigrationsHistoryTable,
                JanusDbContext.Schema)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DataConnections>();

        services.AddSingleton<RandomNumberGenerator>(_ => RandomNumberGenerator.Create());

        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<ISubjectKeyStore, SubjectKeyStore>();
        services.AddScoped<IErasureStore, ErasureStore>();
        services.AddScoped<ISubjectEraser, SubjectEraser>();
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddScoped<IIdentifierStore>(provider => new IdentifierStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            fingerprintKey,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IProfileStore>(provider => new ProfileStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IProfilePhotoStore>(provider => new ProfilePhotoStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IPreferenceStore>(provider => new PreferenceStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IAuditStore>(provider => new AuditStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));

        services.AddScoped<IRoleStore, RoleStore>();
        services.AddScoped<IGrantStore, GrantStore>();
        services.AddScoped<IGroupStore, GroupStore>();
        services.AddScoped<IResourceStore, ResourceStore>();

        services.AddScoped<IAccessEvaluator, AccessEvaluator>();
        services.AddScoped<IIndexCatalogue, IndexCatalogue>();
        services.AddScoped<ISubjectRestrictions, SubjectRestrictions>();
        services.AddScoped<IAccessAudit, AccessAudit>();

        return services;
    }
}
