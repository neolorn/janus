using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Credentials;

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
/// <param name="signOn">
/// Which client of the provider this application is, or nothing where the deployment
/// registered none.
/// </param>
/// <param name="mail">The mail server, or nothing where the deployment hosts no mailbox.</param>
/// <param name="mailClient">
/// Which client of the provider the mail server is, or nothing where the deployment
/// registered none.
/// </param>
/// <param name="codec">
/// What the deployment reads uploaded images with, or nothing where it registered
/// none.
/// </param>
/// <param name="providers">The social providers whose security events the deployment takes.</param>
/// <param name="configuration">Where the organizations that show photos are read.</param>
/// <remarks>
/// Implements LIB-HOST-001, REG-PM-001, AUTH-SESS-012, BFF-SESS-006, IDN-ATTR-002,
/// INT-MAIL-010 and IDN-LIFE-012a.
/// The library knows no route of the frontend, so it has none to fall back on: a
/// deployment that declares none of these is stopped here rather than answering a
/// password manager as a site that offers neither page, meeting an interactive
/// authorization request with nowhere to send it, or reaching the first person who
/// arrives holding nothing without knowing what to call itself at the provider. The
/// codec is optional until a policy shows photos, and required from then on, because
/// the library reads no image itself. The mail server's client is optional until a mail
/// server is registered, and required from then on, because which protocol client the
/// server trusts is the deployment's to say. A social provider is optional, and one
/// declared is declared whole: named once, as a social provider, with the HTTPS address
/// of its document and at least one client, since a declaration short of that would
/// verify none of the events it was declared for.
/// </remarks>
internal sealed class DeclarationCoverage(
    PasskeyAddresses? addresses,
    AuthenticationAddresses? authentication,
    SignOnClient? signOn,
    IMailServer? mail,
    MailServerClient? mailClient,
    ImageCodec? codec,
    IEnumerable<SocialProvider> providers,
    IConfigurationStore configuration)
{
    private const string Passkeys = "passkeyAddresses";

    private const string Authentication = "authenticationAddresses";

    private const string Client = "signOnClient.clientId";

    private const string Codec = "imageCodec";

    private const string MailClient = "mailServerClient.clientId";

    private const string Social = "socialProvider";

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

        if (addresses.ChangePassword.Length is 0)
        {
            return Missing(Passkeys + ".changePassword");
        }

        if (addresses.Enrol.Length is 0)
        {
            return Missing(Passkeys + ".enrol");
        }

        if (addresses.Manage.Length is 0)
        {
            return Missing(Passkeys + ".manage");
        }

        if (authentication is null || authentication.SignIn.Length is 0)
        {
            return Missing(Authentication + ".signIn");
        }

        if (authentication.Provider.Length is 0)
        {
            return Missing(Authentication + ".provider");
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
        if (mail is not null && (mailClient is null || mailClient.ClientId.Length is 0))
        {
            return Missing(MailClient);
        }

        if (Undeclared() is string part)
        {
            return Missing(Social + "." + part);
        }

        return await PhotographedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result Missing(string key) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(key)));

    // IDN-LIFE-012, IDN-LIFE-012a: the part of a social provider's declaration that
    // does not hold, or nothing where every one holds.
    private string? Undeclared()
    {
        var named = new HashSet<Factor>();

        foreach (SocialProvider declared in providers)
        {
            if (!named.Add(declared.Provider)
                || FactorCatalogue.Of(declared.Provider).AssuranceLevel is not AssuranceLevel.Delegated)
            {
                return "provider";
            }

            if (declared.Metadata is not { IsAbsoluteUri: true } metadata || metadata.Scheme != Uri.UriSchemeHttps)
            {
                return "metadata";
            }

            if (declared.ClientIds is not { Count: > 0 } clients || clients.Any(string.IsNullOrWhiteSpace))
            {
                return "clientIds";
            }

            // IDN-LIFE-012, REG-IDENT-008: a provider people sign in with is read from
            // its discovery document, returns them to the library's own route for it,
            // and is presented a secret at the exchange.
            if (declared.Configuration is not { IsAbsoluteUri: true } configuration
                || configuration.Scheme != Uri.UriSchemeHttps)
            {
                return "configuration";
            }

            if (declared.Return is not { IsAbsoluteUri: true } returned
                || returned.Scheme != Uri.UriSchemeHttps
                || !ProviderRoutes.Named.Any(route =>
                    route.Value == declared.Provider
                    && returned.AbsolutePath.EndsWith(
                        "/callbacks/providers/" + route.Key + "/return",
                        StringComparison.Ordinal)))
            {
                return "return";
            }

            if (declared.Secret.IsEmpty)
            {
                return "secret";
            }
        }

        return null;
    }

    // IDN-ATTR-002: a photo is available where an organization's policy says so, and
    // the library has nothing to make one with unless the deployment declared a codec.
    private async ValueTask<Result> PhotographedAsync(CancellationToken cancellationToken)
    {
        if (codec is not null)
        {
            return Result.Success();
        }

        Error? failure = null;
        bool shown = false;

        (await configuration
                .ReadWrittenAsync(Settings.OrganizationPhoto, cancellationToken)
                .ConfigureAwait(false))
            .Switch(
                written => shown = written.Any(organization => organization.Value),
                error => failure = error);

        if (failure is Error unreadable)
        {
            return Result.Failure(unreadable);
        }

        return shown ? Missing(Codec) : Result.Success();
    }
}
