using System;
using System.Security.Cryptography;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Oidc;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Accounts;
using Janus.Identity.Audit;
using Janus.Identity.Identifiers;
using Janus.Identity.Organizations;
using Janus.Identity.Preferences;
using Janus.Identity.Profiles;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Erasures;
using Janus.Privacy.Exports;
using Janus.Privacy.Records;
using Janus.Privacy.Requests;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Alerting;
using Janus.Storage.Authentication.Configuration;
using Janus.Storage.Authentication.Credentials;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Oidc;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Authentication.Policies;
using Janus.Storage.Authentication.Recovery;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Authentication.Sending;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Authentication.SignIn;
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
using Janus.Storage.Privacy;
using Janus.Storage.Privacy.Consents;
using Janus.Storage.Privacy.Documents;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.Exports;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.Policies;
using Janus.Storage.Privacy.Records;
using Janus.Storage.Privacy.Requests;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
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
        services.AddSingleton<IRegistrationSignals>(provider => new RegistrationSignals(
            connectionString,
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<DataConnections>();

        services.AddSingleton<RandomNumberGenerator>(_ => RandomNumberGenerator.Create());

        services.AddScoped<IConfigurationStore, ConfigurationStore>();

        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<ISubjectKeyStore>(provider => new SubjectKeyStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IErasureStore, ErasureStore>();
        services.AddScoped<ISubjectEraser, SubjectEraser>();
        services.AddScoped<Janus.Privacy.Outbox.IOutboxStore, OutboxStore>();
        services.AddScoped<IPrivacyRequestStore, PrivacyRequestStore>();
        services.AddScoped<IAccountStates, AccountStates>();
        services.AddScoped<IExportSource, ExportSource>();
        services.AddScoped<IExportLedger, ExportLedger>();
        services.AddScoped<IComplianceStore, ComplianceStore>();
        services.AddScoped<IRegisterRoles, RegisterRoles>();
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddScoped<Janus.Privacy.Erasures.IOrganizationStates, OrganizationStates>();
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

        services.AddScoped<ISessionStore>(provider => new SessionStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IAuthenticatorStore>(provider => new AuthenticatorStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IRegistrationSessionStore>(provider => new RegistrationSessionStore(
            provider.GetRequiredService<JanusDbContext>(),
            provider.GetRequiredService<DataConnections>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IRegistrationDirectory>(provider => new RegistrationDirectory(
            provider.GetRequiredService<JanusDbContext>(),
            provider.GetRequiredService<IAccountStore>(),
            provider.GetRequiredService<IIdentifierStore>(),
            provider.GetRequiredService<IProfileStore>(),
            provider.GetRequiredService<ISubjectKeyStore>()));
        services.AddScoped<IPendingVerificationStore>(provider => new PendingVerificationStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IIdentifierDirectory, IdentifierDirectory>();
        services.AddScoped<IAccountDirectory, AccountDirectory>();
        services.AddScoped<ILifecycleLinkStore, LifecycleLinkStore>();
        services.AddScoped<IAccountAudit, AccountAudit>();
        services.AddScoped<IPasswordStore, PasswordStore>();
        services.AddScoped<IRecoveryCodeStore, RecoveryCodeStore>();
        services.AddScoped<IDeviceStore, DeviceStore>();
        services.AddScoped<IMembershipLookup, MembershipLookup>();
        services.AddScoped<IPreAuthenticationStore, PreAuthenticationStore>();
        services.AddScoped<IChallengeStore, ChallengeStore>();
        services.AddScoped<IVerificationCodeStore, VerificationCodeStore>();
        services.AddScoped<IKeyCeremonyStore, KeyCeremonyStore>();
        services.AddScoped<IPendingSignInStore>(provider => new PendingSignInStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IPolicyRaiseStore, PolicyRaiseStore>();
        services.AddScoped<IRecoveryLinkStore, RecoveryLinkStore>();
        services.AddScoped<IRecoveryApprovalStore>(provider => new RecoveryApprovalStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<ILossReportStore>(provider => new LossReportStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<IOidcClientStore, OidcClientStore>();
        services.AddScoped<IAuthorizationCodeStore, AuthorizationCodeStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<ISigningKeyStore>(provider => new SigningKeyStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys));
        services.AddScoped<IOidcAudit, OidcAudit>();
        services.AddScoped<IRecoveryAudit, RecoveryAudit>();
        services.AddScoped<ISessionAudit, SessionAudit>();
        services.AddScoped<ICredentialAudit, CredentialAudit>();
        services.AddScoped<ILegalDocumentStore, LegalDocumentStore>();
        services.AddScoped<IConsentStore, ConsentStore>();
        services.AddScoped<Janus.Privacy.IPrivacyAudit, PrivacyAudit>();
        services.AddScoped<Janus.Privacy.Policies.IMembershipLookup, PrivacyMembershipLookup>();

        services.AddScoped<IRoleStore, RoleStore>();
        services.AddScoped<IGrantStore, GrantStore>();
        services.AddScoped<IGroupStore, GroupStore>();
        services.AddScoped<IResourceStore, ResourceStore>();

        services.AddScoped<IAccessEvaluator, AccessEvaluator>();
        services.AddScoped<IIndexCatalogue, IndexCatalogue>();
        services.AddScoped<ISubjectRestrictions, SubjectRestrictions>();
        services.AddScoped<IRecordedConsents, RecordedConsents>();
        services.AddScoped<IAccessAudit, AccessAudit>();

        services.AddScoped<ISendOutbox>(provider => new SendDeliveryStore(
            provider.GetRequiredService<JanusDbContext>(),
            keyEncryptionKeys,
            provider.GetRequiredService<RandomNumberGenerator>()));
        services.AddScoped<ISendLedger>(provider => new SendLedger(
            provider.GetRequiredService<JanusDbContext>(),
            fingerprintKey));
        services.AddScoped<IThrottleLedger>(provider => new ThrottleLedger(
            provider.GetRequiredService<JanusDbContext>(),
            fingerprintKey));
        services.AddScoped<INoticeLedger>(provider => new NoticeLedger(
            provider.GetRequiredService<JanusDbContext>(),
            fingerprintKey));
        services.AddScoped<ICallbackLedger>(provider => new CallbackLedger(
            provider.GetRequiredService<JanusDbContext>(),
            fingerprintKey));
        services.AddScoped<IRegistrationSources>(provider => new RegistrationSourceLedger(
            provider.GetRequiredService<JanusDbContext>(),
            fingerprintKey));
        services.AddScoped<ISmsBalanceLedger, SmsBalanceLedger>();
        services.AddScoped<IAlertLedger, AlertLedger>();
        services.AddScoped<IConfigurationAudit, ConfigurationAudit>();
        services.AddScoped<ISendAudit, SendAudit>();
        services.AddScoped<IBotDefenceAudit, BotDefenceAudit>();
        services.AddScoped<IPhoneSignalAudit, PhoneSignalAudit>();

        return services;
    }
}
