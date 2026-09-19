using System;
using Janus.Core;
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
    /// Registers the context, the unit of work and the connection accessor.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="connectionString">
    /// The application's own credential, which holds row-level access and no schema
    /// right (OPS-MIG-003).
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    public static IServiceCollection AddJanusStorage(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<JanusDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                JanusDbContext.MigrationsHistoryTable,
                JanusDbContext.Schema)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DataConnections>();

        return services;
    }
}
