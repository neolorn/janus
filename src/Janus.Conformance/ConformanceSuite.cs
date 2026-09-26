using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting;
using Janus.Hosting.Bff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Conformance;

/// <summary>
/// The conformance suite a host runs against its own configuration: its entities, its
/// model declaration, its truth table and its provider.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-001, CONV-TEST-002 and AUTH-OIDC-006 AC1. Each part answers a
/// report whose findings are codes with structured data, so the host's own test
/// asserts the report conforms and states any finding in its own words. The truth
/// table writes rows into the deployment's database, so the suite is run against a
/// deployment kept for it and never against one serving people.
/// </remarks>
public static class ConformanceSuite
{
    // The declaration check builds no provider, so nothing it registers is ever
    // reached; the address is one no name resolves to (RFC 2606).
    private const string Unreached = "Host=conformance.invalid";

    /// <summary>
    /// Whether every entity the host's context maps has a registered policy.
    /// </summary>
    /// <param name="services">The host's deployment, as it registered the library.</param>
    /// <param name="model">The model of the host's own context.</param>
    /// <returns>
    /// A finding, <c>authz.policy.unregistered</c> naming the entity, for each entity
    /// that is neither a declared resource type, nor the rows of a declared
    /// relationship, nor one of the two contract tables.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is absent.</exception>
    /// <remarks>
    /// Implements LIB-TEST-001 AC1, AUTHZ-GATE-001 AC3 and CONV-TEST-003. An owned type
    /// is read only through its owner, so it is its owner's policy that covers it.
    /// </remarks>
    public static ConformanceReport Policies(IServiceProvider services, IModel model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);

        AuthorizationDeclaration declaration = services.GetRequiredService<AuthorizationDeclaration>();

        var policed = new HashSet<Type>(
        [
            .. declaration.ResourceTypes.Select(type => type.Entity),
            .. declaration.Relationships.Select(relationship => relationship.Holder.Parameters[0].Type),
            typeof(AncestryEntry),
            typeof(EffectiveGrant),
        ]);

        return new ConformanceReport(
        [
            .. model.GetEntityTypes()
                .Where(entity => !entity.IsOwned())
                .Select(entity => entity.ClrType)
                .Distinct()
                .Where(entity => !policed.Contains(entity))
                .OrderBy(entity => entity.FullName, StringComparer.Ordinal)
                .Select(entity => new ConformanceFinding(
                    ConformanceCheck.Policies,
                    Error.From(
                        ErrorCodes.PolicyUnregistered,
                        "entity",
                        JsonSerializer.SerializeToElement(entity.FullName)))),
        ]);
    }

    /// <summary>
    /// Whether a model declaration holds together, judged by the checks the library
    /// runs on it as it starts.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>
    /// Nothing, or a finding carrying the refusal's own code and details, so each
    /// kind of failure is reported as itself.
    /// </returns>
    /// <exception cref="ArgumentNullException">The declaration is absent.</exception>
    /// <exception cref="StartupException">The declaration is refused for a reason no code names.</exception>
    /// <remarks>
    /// Implements LIB-TEST-001 AC3 and AUTHZ-MODEL-004. The declaration is judged by
    /// the registration a host makes, over key material drawn for the check alone and
    /// cleared after it, so the checks are the library's and not a second copy of
    /// them. Like startup, the judgement stops at the first failure, so a declaration
    /// that fails two ways reports the second once the first is corrected.
    /// </remarks>
    public static ConformanceReport Declaration(AuthorizationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return new ConformanceReport(Validated(declaration).Match<IReadOnlyList<ConformanceFinding>>(
            () => [],
            failure => [new ConformanceFinding(ConformanceCheck.Declaration, failure)]));
    }

    /// <summary>
    /// Runs the host's truth table for one resource type through the single check and
    /// the list filter, and asks whether both decide every case as the table states.
    /// </summary>
    /// <typeparam name="TResource">The host's type of the records asked about.</typeparam>
    /// <param name="services">The host's deployment, as it registered the library.</param>
    /// <param name="connect">
    /// Opens a connection to the deployment's database, which the suite writes the
    /// library's rows for each case through and closes.
    /// </param>
    /// <param name="rows">The host's own rows of the type.</param>
    /// <param name="cases">The table.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>
    /// A finding, <c>authz.truthtable.disagreement</c> naming the case and what each
    /// path decided, for each case that either path decided otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The deployment declares no such type, or a case names a scenario its declaration
    /// does not place it in.
    /// </exception>
    /// <remarks>Implements LIB-TEST-001 AC2, AUTHZ-TEST-001 and AUTHZ-PRIN-001.</remarks>
    public static async ValueTask<ConformanceReport> TruthTableAsync<TResource>(
        IServiceProvider services,
        Func<CancellationToken, ValueTask<DbConnection>> connect,
        IConformanceRows<TResource> rows,
        IReadOnlyList<TruthTableCase> cases,
        CancellationToken cancellationToken)
        where TResource : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(rows);

        var library = new CaseRows(connect, services.GetRequiredService<TimeProvider>());

        return await new TruthTable<TResource>(services, library, rows)
            .RunAsync(cases, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the deployment's provider each refusal AUTH-OIDC-006 names, as a registered
    /// client would ask it.
    /// </summary>
    /// <param name="client">What reaches the deployment.</param>
    /// <param name="issuer">The provider's issuer, which its discovery document sits under.</param>
    /// <param name="registered">A client the deployment's registry holds.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>
    /// A finding, <c>auth.oidc.nonconformant</c> naming the probe, what was sent, the
    /// refusal expected and what came back, for each form the provider admitted or
    /// its document advertised.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is absent.</exception>
    /// <remarks>
    /// Implements AUTH-OIDC-006 AC1 and LIB-TEST-001. The implicit and hybrid forms,
    /// every grant beside the code and the refresh, the plain proof key and no proof
    /// key, and a client that does not authenticate are each asked for and must be
    /// refused. The exact-match rule and the code exchange need a person signed in, so
    /// they are the library's own tests and not the host's.
    /// </remarks>
    public static async ValueTask<ConformanceReport> ProviderAsync(
        HttpClient client,
        Uri issuer,
        ConformanceClient registered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(registered);

        return await new ProviderProbe(client, issuer, registered)
            .RunAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Result Validated(AuthorizationDeclaration declaration)
    {
        byte[] encryption = RandomNumberGenerator.GetBytes(32);
        byte[] fingerprint = RandomNumberGenerator.GetBytes(FingerprintKeys.MinimumLength);
        byte[] signOn = RandomNumberGenerator.GetBytes(32);

        try
        {
            _ = new ServiceCollection().AddJanus(
                Unreached,
                new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = encryption }),
                new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = fingerprint }),
                signOn,
                Encoding.UTF8.GetBytes(Unreached),
                declaration,
                ApplicationKind.Public);

            return Result.Success();
        }
        catch (StartupException refused) when (refused.Failure is not null)
        {
            return Result.Failure(refused.Failure);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryption);
            CryptographicOperations.ZeroMemory(fingerprint);
            CryptographicOperations.ZeroMemory(signOn);
        }
    }
}
