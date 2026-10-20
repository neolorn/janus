using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting;

/// <summary>
/// What LIB-HOST-001 requires a host to declare, read against what it registered.
/// </summary>
/// <param name="addresses">
/// The frontend pages the two well-known documents point at, or nothing where the
/// deployment registered none.
/// </param>
/// <param name="authentication">
/// Where a browser holding no session is sent, or nothing where the deployment
/// registered none.
/// </param>
/// <param name="landing">
/// Where a link the library sends lands, or nothing where the deployment registered
/// none.
/// </param>
/// <param name="signOn">
/// Which client of the provider this application is, or nothing where the deployment
/// registered none.
/// </param>
/// <param name="mail">The mail server in use, where the deployment has one.</param>
/// <param name="mailClient">
/// Which client of the provider the mail server is, or nothing where the deployment
/// registered none.
/// </param>
/// <param name="codec">
/// What the deployment reads uploaded images with, or nothing where it registered
/// none.
/// </param>
/// <param name="dns">
/// Where a listed domain's TXT record is read, or nothing where the deployment
/// registered no resolver.
/// </param>
/// <param name="providers">The social providers whose security events the deployment takes.</param>
/// <param name="sources">The relationship sources the host registered.</param>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <param name="subscribers">The subject-event subscribers the host registered.</param>
/// <param name="handlers">The purpose handlers the host registered.</param>
/// <param name="contributors">The export contributors the host registered.</param>
/// <param name="clients">The clients registered with the provider.</param>
/// <param name="configuration">
/// Where the stored policies that show photos, and the domains each lists, are read.
/// </param>
/// <param name="scope">The scope the check runs in, which gives a host context as a request does.</param>
/// <remarks>
/// Implements LIB-HOST-001, REG-PM-001, AUTH-SESS-012, BFF-SESS-006, IDN-ATTR-002,
/// INT-MAIL-010, IDN-LIFE-012a, REG-DOM-001, INT-SMS-003, API-LAND-001, AUTHZ-DERIVE-005,
/// AUTHZ-DERIVE-007, PRIV-CONS-006a and PRIV-RIGHT-003.
/// The library knows no route of the frontend, so it has none to fall back on: a
/// deployment that declares none of these is stopped here rather than answering a
/// password manager as a site that offers neither page, meeting an interactive
/// authorization request with nowhere to send it, or reaching the first person who
/// arrives holding nothing without knowing what to call itself at the provider. The
/// codec is optional until a stored policy shows photos, the system's or an
/// organization's, and required from then on, because the library reads no image
/// itself; the DNS resolver likewise until a lock lists a domain, because the library
/// looks up no record itself. The mail server's client is optional until a mail server
/// is registered, and required from then on, because which protocol client the server
/// trusts is the deployment's to say. A social provider is
/// optional, and one declared is declared whole: named once, as a social provider, with
/// the HTTPS address of its document and at least one client, since a declaration short
/// of that would verify none of the events it was declared for; one that is malformed is
/// refused as invalid rather than missing, naming the provider and the member at fault
/// (D-175). A governing document's name and a subscriber's or a purpose handler's fill
/// a message place measured at the width the name rule bounds, so one declared outside
/// the rule is refused here rather than carried into a text longer than the width it
/// was measured at; an export contributor is held to the same rule and to a name of its
/// own, since contributors are asked in the order of their names and a failing one is
/// named by it (D-206). A landing origin is an application of this deployment, so one that
/// no registered browser client returns to, or an authentication origin that is not
/// where the sign-in address is, would send every link somewhere the deployment does
/// not serve. A relationship a derivation follows from is read by the view of who can
/// access a record and by the drift check through the source the host declares for it,
/// so one with no source, or a source that does not fit its relationship or names a
/// context the library cannot read through, stops the deployment here (D-166, D-183).
/// A cookie the host declares is listed in the privacy notice beside the library's
/// own, so one under a name that is no cookie name, under a library cookie's name or
/// declared twice, or serving a purpose that does not rest on consent, is refused here
/// (D-203).
/// </remarks>
internal sealed class DeclarationCoverage(
    PasskeyAddresses? addresses,
    AuthenticationAddresses? authentication,
    LandingOrigins? landing,
    SignOnClient? signOn,
    IMailServerInUse mail,
    MailServerClient? mailClient,
    ImageCodec? codec,
    IDnsResolver? dns,
    IEnumerable<SocialProvider> providers,
    IEnumerable<RelationshipSource> sources,
    AuthorizationDeclaration declaration,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IEnumerable<IPurposeHandler> handlers,
    IEnumerable<IExportContributor> contributors,
    IOidcClientStore clients,
    IConfigurationStore configuration,
    IServiceProvider scope)
{
    private const string Passkeys = "passkeyAddresses";

    private const string Authentication = "authenticationAddresses";

    private const string Client = "signOnClient.clientId";

    private const string LandingAuthentication = "landingOrigins.authentication";

    private const string LandingAccount = "landingOrigins.account";

    private const string Codec = "imageCodec";

    private const string Resolver = "dnsResolver";

    private const string MailClient = "mailServerClient.clientId";

    private const string Social = "socialProvider";

    private const string Source = "relationshipSource";

    private const string Cookie = "cookie";

    private const string Contributor = "contributor";

    // RFC 2616 section 2.2: the characters a token never holds, space and tab aside,
    // which fall outside the visible range.
    private const string Separators = "()<>@,;:\\\"/[]?={}";

    /// <summary>
    /// Reads what LIB-HOST-001 requires against what is registered.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the first omission, named.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        if (addresses is null)
        {
            return Missing(Passkeys);
        }

        if (string.IsNullOrWhiteSpace(addresses.ChangePassword))
        {
            return Missing(Passkeys + ".changePassword");
        }

        if (string.IsNullOrWhiteSpace(addresses.Enrol))
        {
            return Missing(Passkeys + ".enrol");
        }

        if (string.IsNullOrWhiteSpace(addresses.Manage))
        {
            return Missing(Passkeys + ".manage");
        }

        if (authentication is null || string.IsNullOrWhiteSpace(authentication.SignIn))
        {
            return Missing(Authentication + ".signIn");
        }

        if (string.IsNullOrWhiteSpace(authentication.Provider))
        {
            return Missing(Authentication + ".provider");
        }

        if (landing is null || landing.Authentication.Length is 0)
        {
            return Missing(LandingAuthentication);
        }

        if (landing.Account.Length is 0)
        {
            return Missing(LandingAccount);
        }

        // BFF-SESS-006: every application of a deployment is a client of the one
        // provider, and which one this process is is not something the library can
        // work out from anything else it holds.
        if (signOn is null || signOn.ClientId.Length is 0)
        {
            return Missing(Client);
        }

        // INT-MAIL-010: the app passwords of a hosted mailbox are reached with a token
        // issued to the mail server's client, and nothing else says which client it is.
        if (mail.Chosen().Match(_ => true, _ => false) && (mailClient is null || mailClient.ClientId.Length is 0))
        {
            return Missing(MailClient);
        }

        if (Malformed() is (string declared, string field))
        {
            return Invalid(declared, field);
        }

        if (Unruled() is (string named, string member))
        {
            return Invalid(named, member);
        }

        if (Uncookied() is (string cookie, string at))
        {
            return Invalid(cookie, at);
        }

        if (Sourced().Match<Error?>(() => null, error => error) is Error unsourced)
        {
            return Result.Failure(unsourced);
        }

        if (await UnlandedAsync(landing, authentication.SignIn, cancellationToken).ConfigureAwait(false)
            is string unlanded)
        {
            return Result.Failure(Error.From(
                ErrorCodes.StartupDeclarationInvalid,
                "key",
                JsonSerializer.SerializeToElement(unlanded)));
        }

        if ((await PhotographedAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unphotographed)
        {
            return Result.Failure(unphotographed);
        }

        return await ResolvedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result Missing(string key) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(key)));

    private static Result Invalid(string declaration, string field) =>
        Result.Failure(new Error(
            ErrorCodes.StartupDeclarationInvalid,
            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
            {
                ["declaration"] = JsonSerializer.SerializeToElement(declaration),
                ["field"] = JsonSerializer.SerializeToElement(field),
            }));

    // IDN-LIFE-012, IDN-LIFE-012a, D-175: the social provider declaration that does not
    // hold, as the refusal of its credential names it, and its member at fault, or
    // nothing where every one holds.
    private (string Declaration, string Field)? Malformed()
    {
        var named = new HashSet<Factor>();

        foreach (SocialProvider declared in providers)
        {
            string declaration = Social + "." + ProviderRoutes.NameOf(declared.Provider);

            if (!named.Add(declared.Provider)
                || FactorCatalogue.Of(declared.Provider).AssuranceLevel is not AssuranceLevel.Delegated)
            {
                return (declaration, "provider");
            }

            if (declared.Metadata is not { IsAbsoluteUri: true } metadata || metadata.Scheme != Uri.UriSchemeHttps)
            {
                return (declaration, "metadata");
            }

            if (declared.ClientIds is not { Count: > 0 } clients || clients.Any(string.IsNullOrWhiteSpace))
            {
                return (declaration, "clientIds");
            }

            // IDN-LIFE-012, REG-IDENT-008: a provider people sign in with is read from
            // its discovery document and returns them to the library's own route for it.
            if (declared.Configuration is not { IsAbsoluteUri: true } configuration
                || configuration.Scheme != Uri.UriSchemeHttps)
            {
                return (declaration, "configuration");
            }

            if (declared.Return is not { IsAbsoluteUri: true } returned
                || returned.Scheme != Uri.UriSchemeHttps
                || !ProviderRoutes.Named.Any(route =>
                    route.Value == declared.Provider
                    && returned.AbsolutePath.EndsWith(
                        "/callbacks/providers/" + route.Key + "/return",
                        StringComparison.Ordinal)))
            {
                return (declaration, "return");
            }
        }

        return null;
    }

    // An origin is the scheme, host and port alone, written as a browser writes it, so
    // one address stands for it and nothing else does; anything else is no origin.
    private static string? Origin(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out Uri? parsed)
            ? parsed.GetLeftPart(UriPartial.Authority)
            : null;

    private static bool IsOrigin(string declared) =>
        Uri.TryCreate(declared, UriKind.Absolute, out Uri? parsed)
        && parsed.Scheme == Uri.UriSchemeHttps
        && string.Equals(parsed.GetLeftPart(UriPartial.Authority), declared, StringComparison.Ordinal);

    // LIB-HOST-001 AC6, API-LAND-001: the landing origin that is not an https origin, is
    // not where a registered browser client returns to, or, for the authentication
    // application, is not where the sign-in address is, as its key; or nothing.
    private async ValueTask<string?> UnlandedAsync(
        LandingOrigins declared,
        string signIn,
        CancellationToken cancellationToken)
    {
        HashSet<string> served = [.. (await clients.AllAsync(cancellationToken).ConfigureAwait(false))
            .Where(client => client.Kind is OidcClientKind.BrowserApplication)
            .Select(client => Origin(client.Redirect))
            .OfType<string>()];

        if (!IsOrigin(declared.Authentication)
            || !served.Contains(declared.Authentication)
            || !string.Equals(Origin(signIn), declared.Authentication, StringComparison.Ordinal))
        {
            return LandingAuthentication;
        }

        return IsOrigin(declared.Account) && served.Contains(declared.Account) ? null : LandingAccount;
    }

    // INT-SMS-003: the governing document a purpose names, as the purpose it is
    // declared on, and the subscriber or purpose handler whose name breaks the rule of a
    // message place, as the name it is registered under, and the export contributor whose
    // name breaks it or is given twice, under the contributor's prefix, or nothing where
    // every one keeps it (D-203, D-206).
    private (string Declaration, string Field)? Unruled()
    {
        foreach (PurposeDeclaration purpose in declaration.ResourceTypes.SelectMany(type => type.Purposes))
        {
            if (purpose.Document is string document && !PlaceName.Holds(document))
            {
                return (purpose.Name, "document");
            }
        }

        foreach (ISubjectEventSubscriber subscriber in subscribers)
        {
            if (!PlaceName.Holds(subscriber.Name))
            {
                return (subscriber.Name, "name");
            }
        }

        foreach (IPurposeHandler handler in handlers)
        {
            if (!PlaceName.Holds(handler.Name))
            {
                return (handler.Name, "name");
            }
        }

        // PRIV-RIGHT-003, LIB-HOST-001 (D-206): contributors are asked in the order of
        // their names and a failing one is named by it, so each holds a name of its own.
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (IExportContributor contributor in contributors)
        {
            if (!PlaceName.Holds(contributor.Name) || !named.Add(contributor.Name))
            {
                return (Contributor + "." + contributor.Name, "name");
            }
        }

        return null;
    }

    // PRIV-CONS-006a, LIB-HOST-001 (D-203): the declared cookie that is no cookie name, a
    // name the library's own cookies take or one declared twice, as its name, or one
    // serving a purpose not declared or not resting on consent, as its purpose; or
    // nothing where every one holds. A cookie that is not strictly necessary rests on
    // the consent its purpose asks for, so a purpose on any other basis would set it
    // for everyone.
    private (string Declaration, string Field)? Uncookied()
    {
        var consenting = new HashSet<string>(
            declaration.LawfulBases.Where(basis => basis.IsConsent).Select(basis => basis.Key),
            StringComparer.Ordinal);
        var consented = new HashSet<string>(
            declaration.ResourceTypes
                .SelectMany(type => type.Purposes)
                .Where(purpose => consenting.Contains(purpose.Basis))
                .Select(purpose => purpose.Name),
            StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (CookieDeclaration cookie in declaration.Cookies)
        {
            string declared = Cookie + "." + cookie.Name;

            if (!IsCookieName(cookie.Name)
                || DeploymentCookies.Library.Contains(cookie.Name)
                || !named.Add(cookie.Name))
            {
                return (declared, "name");
            }

            if (cookie.Purpose is string purpose && !consented.Contains(purpose))
            {
                return (declared, "purpose");
            }
        }

        return null;
    }

    // RFC 6265 section 4.1.1: a cookie's name is a token of RFC 2616 section 2.2, one or
    // more visible US-ASCII characters none of which is a separator.
    private static bool IsCookieName(string name) =>
        name.Length > 0
        && name.All(character =>
            character is > ' ' and < '\u007f' && !Separators.Contains(character, StringComparison.Ordinal));

    // LIB-HOST-001, AUTHZ-DERIVE-005 AC5: a source is given once, for a relationship the
    // model declares, answers the row type that relationship's selectors are declared
    // over, and names a context the container gives in a scope and whose model maps the
    // contract tables; and every relationship a derivation follows from, materialised or
    // not, has one. The context's model is built and no query is run.
    private Result Sourced()
    {
        var given = new HashSet<string>(StringComparer.Ordinal);

        foreach (RelationshipSource source in sources)
        {
            string declared = Source + "." + source.Relationship;

            if (!given.Add(source.Relationship)
                || declaration.Relationships.FirstOrDefault(each =>
                        string.Equals(each.Name, source.Relationship, StringComparison.Ordinal))
                    is not RelationshipDeclaration relationship)
            {
                return Invalid(declared, "relationship");
            }

            if (relationship.Holder.Parameters[0].Type != source.Row)
            {
                return Invalid(declared, "rows");
            }

            if (scope.GetService(source.Context) is not DbContext context
                || context.Model.FindEntityType(typeof(AncestryEntry)) is null
                || context.Model.FindEntityType(typeof(EffectiveGrant)) is null
                || context.Model.FindEntityType(typeof(ConsentedResource)) is null)
            {
                return Invalid(declared, "context");
            }
        }

        foreach (string followed in declaration.ResourceTypes
            .SelectMany(type => type.Derivations)
            .Select(derivation => derivation.Relationship))
        {
            if (!given.Contains(followed))
            {
                return Missing(followed);
            }
        }

        return Result.Success();
    }

    // IDN-ATTR-002, OPS-CFG-003: a photo is available where a stored policy's photos
    // field says so, the system policy's or an organization's, and the library has
    // nothing to make one with unless the deployment declared a codec.
    private async ValueTask<Result> PhotographedAsync(CancellationToken cancellationToken)
    {
        if (codec is not null)
        {
            return Result.Success();
        }

        Error? failure = null;
        bool shown = false;

        (await configuration
                .ReadAsync(Settings.PolicyDefault, cancellationToken)
                .ConfigureAwait(false))
            .Switch(system => shown = system.Photos, error => failure = error);

        if (failure is null && !shown)
        {
            (await configuration
                    .ReadWrittenAsync(Settings.OrganizationPolicy, cancellationToken)
                    .ConfigureAwait(false))
                .Switch(
                    written => shown = written.Values.Any(policy => policy.Photos is true),
                    error => failure = error);
        }

        if (failure is Error unreadable)
        {
            return Result.Failure(unreadable);
        }

        return shown ? Missing(Codec) : Result.Success();
    }

    // REG-DOM-001, X6 of D-166: a listed domain is verified and re-verified by its TXT
    // record, and the library reads none unless the deployment declared a resolver.
    private async ValueTask<Result> ResolvedAsync(CancellationToken cancellationToken)
    {
        if (dns is not null)
        {
            return Result.Success();
        }

        Error? failure = null;
        bool listed = false;

        (await configuration
                .ReadWrittenAsync(Settings.OrganizationPolicy, cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                written => listed = written.Values.Any(policy => policy.EmailDomains is { Count: > 0 }),
                error => failure = error);

        if (failure is Error unreadable)
        {
            return Result.Failure(unreadable);
        }

        return listed ? Missing(Resolver) : Result.Success();
    }
}
