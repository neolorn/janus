using System;
using System.Security.Cryptography;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Bases;
using Janus.Privacy.Breaches;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Erasures;
using Janus.Privacy.Exports;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Records;
using Janus.Privacy.Requests;
using Janus.Privacy.Takedowns;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Privacy;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>Implements CONV-DESIGN-007.</remarks>
internal static class PrivacyRegistration
{
    /// <summary>
    /// Registers the checks of what the host declared, the services of the consent,
    /// document, request, takedown, erasure, export and record operations, and the
    /// sweeps and the publisher the worker runs.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddPrivacyArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // LIB-HOST-001, PRIV-RIGHT-005b: what the host declared is read back at startup
        // against the handlers it registered.
        services.AddScoped<HandlerCoverage>();
        services.AddScoped<CategoryRetention>();
        services.AddScoped<ConfigurationCoverage>();

        services.AddScoped<ILegalDocuments, LegalDocumentService>();
        services.AddScoped<AdministrativeScope>();
        services.AddScoped<Supersession>();
        services.AddScoped<IConsents, ConsentService>();
        services.AddScoped<WorkingCalendar>();
        services.AddScoped<RestrictionGrant>();
        services.AddScoped<DeadlineSweep>();
        services.AddScoped<HolidayListWatch>();
        services.AddScoped<IPrivacyRequests, PrivacyRequestService>();
        services.AddScoped<ITakedowns, TakedownService>();

        // DR-016: the off-host ledger is the deployment's to register; one it does not
        // register leaves its erasures completing without a line, the residual R-A13
        // accepts until the tier upgrade.
        services.AddScoped<IErasures>(provider => new ErasureService(
            provider.GetRequiredService<AdministrativeScope>(),
            provider.GetRequiredService<IStepUpGate>(),
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<IErasureStore>(),
            provider.GetServices<ISubjectEventSubscriber>(),
            provider.GetService<IErasureLedger>(),
            provider.GetRequiredService<IPrivacyAudit>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>()));

        services.AddScoped<DeletionSweep>();
        services.AddScoped<OrganizationErasureSweep>();
        services.AddScoped<IExports, ExportService>();
        services.AddScoped<IProcessingRecords, ProcessingRecordsService>();
        services.AddScoped<LawfulBasisSeed>();
        services.AddScoped<IAuditTrail, AuditTrailService>();
        services.AddScoped(provider => new OutboxPublisher(
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<IErasureStore>(),
            provider.GetServices<ISubjectEventSubscriber>(),
            provider.GetService<IErasureLedger>(),
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IPrivacyAlerts>(),
            provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RandomNumberGenerator>()));

        return services;
    }
}
