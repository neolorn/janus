using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

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
/// <param name="codec">
/// What the deployment reads uploaded images with, or nothing where it registered
/// none.
/// </param>
/// <param name="configuration">Where the organizations that show photos are read.</param>
/// <remarks>
/// Implements LIB-HOST-001, REG-PM-001, AUTH-SESS-012, BFF-SESS-006 and IDN-ATTR-002.
/// The library knows no route of the frontend, so it has none to fall back on: a
/// deployment that declares none of these is stopped here rather than answering a
/// password manager as a site that offers neither page, meeting an interactive
/// authorization request with nowhere to send it, or reaching the first person who
/// arrives holding nothing without knowing what to call itself at the provider. The
/// codec is optional until a policy shows photos, and required from then on, because
/// the library reads no image itself.
/// </remarks>
internal sealed class DeclarationCoverage(
    PasskeyAddresses? addresses,
    AuthenticationAddresses? authentication,
    SignOnClient? signOn,
    ImageCodec? codec,
    IConfigurationStore configuration)
{
    private const string Passkeys = "passkeyAddresses";

    private const string Authentication = "authenticationAddresses";

    private const string Client = "signOnClient.clientId";

    private const string Codec = "imageCodec";

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

        return await PhotographedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result Missing(string key) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(key)));

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
