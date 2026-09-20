using System;
using System.IO;
using System.Net.Http;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Bff;
using Janus.Hosting.Passwords;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// The one method a host calls to register the library.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-LAYOUT-002 and AUTHZ-SEAM-001. What the host
/// declares about its own domain is built and checked here, once, so a declaration that
/// does not hold together stops the deployment rather than the first request that reads
/// it (AUTHZ-MODEL-004).
/// </remarks>
public static class JanusRegistration
{
    /// <summary>
    /// Registers the library over the host's database and declared domain.
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
    /// The key the searchable fingerprints are computed under, read from the same place
    /// and held outside the database (PRIV-RIGHT-005c).
    /// </param>
    /// <param name="declaration">What the host declared about its own domain.</param>
    /// <param name="application">
    /// Which of the deployment's applications this process serves, which decides the
    /// one cookie attribute that differs between them (BFF-CSRF-005).
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">The declaration does not hold together.</exception>
    public static IServiceCollection AddJanus(
        this IServiceCollection services,
        string connectionString,
        KeyEncryptionKeys keyEncryptionKeys,
        ReadOnlyMemory<byte> fingerprintKey,
        AuthorizationDeclaration declaration,
        JanusApplication application)
    {
        ArgumentNullException.ThrowIfNull(services);

        // CONV-DESIGN-007: time is injected, and a host that has its own clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddJanusStorage(connectionString, keyEncryptionKeys, fingerprintKey);
        services.AddSingleton(AuthorizationModel.Of(declaration));

        // AUTHZ-GROUP-002: one set per operation, which is what makes ten checks in one
        // request resolve membership once.
        services.AddScoped<SubjectSets>();

        // LIB-HOST-004: the assurance provider is the host's to supply, and a host
        // that supplies none is one where nothing reports what a session has proved.
        services.AddScoped(services => new StepUpGates(
            services.GetRequiredService<AuthorizationModel>(),
            services.GetService<IAssuranceProvider>()));

        // BFF-OWN-001: the browser boundary is the library's, so what issues a cookie
        // and what validates a token are registered here and not left to the host.
        services.AddSingleton(new BrowserSessionCookies(application));
        services.AddScoped<SynchronizerTokens>();
        services.AddScoped<ResourceIsolation>();
        services.AddScoped<CustomRequestHeader>();
        services.AddScoped<OriginValidation>();
        services.AddScoped<RequestSession>();
        services.AddScoped<SessionResolution>();
        services.AddScoped<FirstContact>();
        services.AddScoped<SynchronizerToken>();

        // LIB-HOST-001: what the host declares about its own messaging is the host's.
        // A deployment that declares none of it starts, and the checks that would have
        // read a declaration find nothing to read.
        services.TryAddSingleton(RestrictionKeySuppliers.None);
        services.TryAddSingleton(IntegrationEndpoints.None);
        services.TryAddSingleton(Recipients.Shipped);

        // AUTH-ABUSE-004, OPS-ALERT-001: the one path every message takes, and what
        // decides whether it goes.
        services.AddScoped<SmsBalance>();
        services.AddScoped<SendingService>();
        services.AddScoped(provider => new SendingValidation(
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetService<IMessageTemplates>(),
            provider.GetRequiredService<RestrictionKeySuppliers>(),
            provider.GetRequiredService<IntegrationEndpoints>()));
        services.AddScoped<RestrictionAdministration>();
        services.AddScoped<ThrottleService>();
        services.AddScoped<NonExistenceNotice>();
        services.AddScoped<DeliveryReports>();
        services.AddScoped(services => new BotDefence(
            services.GetRequiredService<IConfigurationStore>(),
            services.GetRequiredService<IDatacenterRanges>(),
            services.GetRequiredService<IRegistrationSources>(),
            services.GetRequiredService<IBotDefenceAudit>(),
            services.GetRequiredService<IUnitOfWork>(),
            services.GetService<ChallengeVerifier>(),
            services.GetRequiredService<TimeProvider>()));
        services.AddScoped<AlertRouter>();
        services.AddScoped<AlertDestinationChange>();
        services.AddScoped<IAlertLog, AlertLog>();

        // AUTH-SESS-001, AUTH-PASS-004, AUTH-FACT-005: the authentication services,
        // each of which reads the settings table for what it enforces.
        services.AddScoped<PolicyResolution>();
        services.AddSingleton<Argon2idHasher>();
        services.AddScoped<IScreeningLog, ScreeningLog>();
        services.AddSingleton<IWordList>(_ => new WordList(Corpus));

        // INT-PWD-001: the range API is reached over the framework's client, which
        // rotates its connections; the corpus files beside the application answer
        // when it cannot (INT-PWD-002).
        services.AddHttpClient<ILeakedPasswordCorpus, LeakedPasswordCorpus>((requests, provider) =>
        {
            requests.BaseAddress = LeakedPasswordCorpus.Provider;

            return new LeakedPasswordCorpus(
                requests,
                provider.GetRequiredService<IConfigurationStore>(),
                provider.GetRequiredService<TimeProvider>(),
                Corpus);
        });

        services.AddScoped<PasswordScreening>();
        services.AddScoped<PasswordService>();
        services.AddScoped<PreAuthenticationService>();
        services.AddScoped<SessionService>();
        services.AddScoped<ISessions>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<TotpService>();
        services.AddScoped<WebAuthnService>();
        services.AddScoped<RecoveryCodeService>();
        services.AddScoped<DeviceService>();
        services.AddScoped<StepUpGuard>();

        services.TryAddSingleton(PreferenceDeclarations.None);
        services.TryAddSingleton(ReservedUsernames.Default);
        services.AddScoped<RegistrationService>();
        services.AddScoped<IRegistration>(provider => provider.GetRequiredService<RegistrationService>());
        services.AddScoped<IdentifierService>();
        services.AddScoped<IIdentifiers>(provider => provider.GetRequiredService<IdentifierService>());
        services.AddScoped<AccountService>();
        services.AddScoped<IAccount>(provider => provider.GetRequiredService<AccountService>());

        services.AddScoped<Derivations>();
        services.AddScoped<IAccessGate, AccessGate>();
        services.AddScoped<IDerivationMaterialiser, DerivationMaterialiser>();
        services.AddScoped<ModelValidation>();

        // AUTHZ-MODEL-004 AC2 (D-160): what a hosted service starts before is what was
        // registered after it, and the web server is one, so the checks that read the
        // database go at the head of the collection.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ModelValidationService>());
        services.Insert(1, ServiceDescriptor.Singleton<IHostedService, SendingValidationService>());

        return services;
    }

    // The corpus and the word list are files a deployment holds beside the
    // application (AUTH-PASS-004, INT-PWD-003).
    private static string Corpus =>
        Path.Combine(AppContext.BaseDirectory, LeakedPasswordCorpus.Directory);
}
