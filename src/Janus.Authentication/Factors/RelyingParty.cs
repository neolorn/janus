using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// The relying party a WebAuthn ceremony runs under: the identifier a credential is
/// bound to, the origins that may run a ceremony, the origins the allowlist is served
/// from, and the signature algorithms accepted.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-010, AUTH-FACT-012 and AUTH-FACT-014. A wrong identifier is
/// only visible once people have enrolled under it, so it is settled at startup and
/// never at the first ceremony.
/// </remarks>
internal sealed class RelyingParty
{
    /// <summary>
    /// How many distinct labels a browser admits across a related-origins allowlist.
    /// </summary>
    public const int LabelLimit = 5;

    private RelyingParty(
        string id,
        IReadOnlyList<string> origins,
        IReadOnlyList<string> relatedOrigins,
        IReadOnlyList<int> algorithms)
    {
        Id = id;
        Origins = origins;
        RelatedOrigins = relatedOrigins;
        Algorithms = algorithms;
    }

    /// <summary>
    /// The identifier every credential is bound to and recorded against.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The origins a ceremony may run from, each of which the identifier sits over.
    /// </summary>
    public IReadOnlyList<string> Origins { get; }

    /// <summary>
    /// The further origins the allowlist is served from, on their own domains.
    /// </summary>
    public IReadOnlyList<string> RelatedOrigins { get; }

    /// <summary>
    /// The COSE algorithms a credential may be created with, in preference order.
    /// </summary>
    public IReadOnlyList<int> Algorithms { get; }

    /// <summary>
    /// Settles the relying party from what the deployment configured, deriving the
    /// identifier where none was named.
    /// </summary>
    /// <param name="identifier">What <c>webauthn.rpid</c> holds, empty where unset.</param>
    /// <param name="origins">What <c>webauthn.origins</c> holds.</param>
    /// <param name="relatedOrigins">What <c>webauthn.relatedorigins</c> holds.</param>
    /// <param name="algorithms">What <c>webauthn.algorithms</c> holds.</param>
    /// <returns>The relying party every ceremony runs under.</returns>
    /// <exception cref="ArgumentNullException">A configured list is absent.</exception>
    /// <exception cref="StartupException">
    /// An origin is not an origin, the identifier does not sit over every origin, or
    /// the allowlist exceeds the label limit.
    /// </exception>
    public static RelyingParty Of(
        string identifier,
        IReadOnlyList<string> origins,
        IReadOnlyList<string> relatedOrigins,
        IReadOnlyList<int> algorithms)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(relatedOrigins);
        ArgumentNullException.ThrowIfNull(algorithms);

        if (origins.Count == 0)
        {
            throw Refused(
                ErrorCodes.StartupRelyingPartyId,
                "origins",
                string.Empty,
                "no origin is configured for it to sit over");
        }

        string[] hosts = [.. origins.Select(Host)];
        string resolved = identifier.Length == 0 ? Common(hosts) : identifier;

        if (Labels(resolved).Length < 2)
        {
            throw Refused(
                ErrorCodes.StartupRelyingPartyId,
                "rpid",
                resolved,
                "it carries no registrable parent domain");
        }

        foreach (string host in hosts)
        {
            if (!Over(resolved, host))
            {
                throw Refused(
                    ErrorCodes.StartupRelyingPartyId,
                    "origin",
                    host,
                    resolved + " is not a registrable suffix of it");
            }
        }

        string[] related = [.. relatedOrigins.Select(Host)];
        HashSet<string> labels = [.. related.Append(resolved).Select(Registrable)];

        return labels.Count > LabelLimit
            ? throw Refused(
                ErrorCodes.StartupLabelLimit,
                "labels",
                string.Join(", ", labels.Order(StringComparer.OrdinalIgnoreCase)),
                "a browser reads no more than "
                + LabelLimit.ToString(CultureInfo.InvariantCulture)
                + " of them from an allowlist")
            : new RelyingParty(resolved, [.. origins], [.. relatedOrigins], [.. algorithms]);
    }

    /// <summary>
    /// Settles the relying party from the deployment's configuration, at startup and
    /// before a ceremony can run.
    /// </summary>
    /// <param name="configuration">Where the four keys are read from.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The relying party every ceremony runs under.</returns>
    /// <exception cref="ArgumentNullException">The store is absent.</exception>
    /// <exception cref="StartupException">
    /// A key holds a value it no longer admits, or the configuration does not hold
    /// together.
    /// </exception>
    public static async ValueTask<RelyingParty> ForAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Of(
            await ValueAsync(configuration, Settings.WebAuthnRelyingPartyId, cancellationToken)
                .ConfigureAwait(false),
            await ValueAsync(configuration, Settings.WebAuthnOrigins, cancellationToken)
                .ConfigureAwait(false),
            await ValueAsync(configuration, Settings.WebAuthnRelatedOrigins, cancellationToken)
                .ConfigureAwait(false),
            await ValueAsync(configuration, Settings.WebAuthnAlgorithms, cancellationToken)
                .ConfigureAwait(false));
    }

    /// <summary>
    /// The related-origins document, which lists the further origins and nothing else.
    /// </summary>
    /// <returns>The document a browser reads at the well-known address.</returns>
    public string Allowlist() =>
        JsonSerializer.Serialize(
            new RelatedOrigins(RelatedOrigins),
            WebAuthnJson.Default.RelatedOrigins);

    /// <summary>
    /// Whether a credential recorded under an identifier is still bound to the one in
    /// force, a change of which asks its owner to enrol again.
    /// </summary>
    /// <param name="recorded">What the credential carries.</param>
    /// <returns>Whether the credential still stands.</returns>
    public bool Binds(string recorded) =>
        string.Equals(Id, recorded, StringComparison.OrdinalIgnoreCase);

    private static string Host(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out Uri? parsed) && parsed.Host.Length > 0
            ? parsed.Host
            : throw Refused(
                ErrorCodes.StartupRelyingPartyId,
                "origin",
                origin,
                "it is not an absolute origin with a host");

    private static string[] Labels(string host) => host.Split('.');

    // The widest door every origin shares: the labels they have in common from the
    // right, which is the parent domain a passkey created for it works across.
    private static string Common(IReadOnlyList<string> hosts)
    {
        string[][] reversed = [.. hosts.Select(host => Labels(host).Reverse().ToArray())];
        int shared = 0;

        while (reversed.All(labels =>
            labels.Length > shared
            && string.Equals(labels[shared], reversed[0][shared], StringComparison.OrdinalIgnoreCase)))
        {
            shared++;
        }

        return string.Join('.', reversed[0].Take(shared).Reverse());
    }

    private static bool Over(string identifier, string host) =>
        string.Equals(host, identifier, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + identifier, StringComparison.OrdinalIgnoreCase);

    // The name a browser counts an allowlist by: the label before the public suffix.
    private static string Registrable(string host)
    {
        string[] labels = Labels(host);

        return labels.Length < 2 ? labels[0] : labels[^2];
    }

    private static async ValueTask<TValue> ValueAsync<TValue>(
        IConfigurationStore configuration,
        Setting<TValue> setting,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TValue value = (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Held<TValue>(error, ref failure));

        return failure is null
            ? value
            : throw new StartupException(
                "The relying party is refused: " + setting.Key + " holds a value the key does not admit.",
                failure);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static StartupException Refused(ErrorCode code, string name, string value, string why) =>
        new(
            "The relying party is refused: " + value + ", because " + why + ".",
            Error.From(code, name, JsonSerializer.SerializeToElement(value)));
}
