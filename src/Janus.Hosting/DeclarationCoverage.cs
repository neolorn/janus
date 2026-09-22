using System;
using System.Text.Json;
using Janus.Core;

namespace Janus.Hosting;

/// <summary>
/// What LIB-HOST-001 requires a host to declare, read against what it registered.
/// </summary>
/// <param name="addresses">
/// The frontend pages the two well-known documents point at, or nothing where the
/// deployment registered none.
/// </param>
/// <remarks>
/// Implements LIB-HOST-001 and REG-PM-001. The library knows no route of the frontend,
/// so it has none to fall back on: a deployment that declares none of these is stopped
/// here rather than answering a password manager as a site that offers neither page.
/// </remarks>
internal sealed class DeclarationCoverage(PasskeyAddresses? addresses)
{
    private const string Declaration = "passkeyAddresses";

    /// <summary>
    /// Reads what LIB-HOST-001 requires against what is registered.
    /// </summary>
    /// <returns>Nothing, or the first omission, named.</returns>
    public Result Validate()
    {
        if (addresses is null)
        {
            return Missing(Declaration);
        }

        if (addresses.ChangePassword.Length is 0)
        {
            return Missing(Declaration + ".changePassword");
        }

        if (addresses.Enrol.Length is 0)
        {
            return Missing(Declaration + ".enrol");
        }

        return addresses.Manage.Length is 0
            ? Missing(Declaration + ".manage")
            : Result.Success();
    }

    private static Result Missing(string key) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(key)));
}
