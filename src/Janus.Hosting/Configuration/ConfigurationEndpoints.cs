using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Configuration;

/// <summary>
/// The runtime configuration endpoints of chapter 09 section 8: reading one key and
/// changing it.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-002, OPS-CFG-004, LIB-API-005, CONV-CODE-006 and
/// CONV-DESIGN-006. Each is one line to <see cref="IConfigurationAdministration"/>,
/// which judges the permission, the direction and the value; a body missing a member
/// it requires is refused before it is called.
/// </remarks>
internal static class ConfigurationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapConfiguration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/config/{key}", ReadAsync));
        _ = SessionRequired.On(endpoints.MapPut("/admin/config/{key}", ChangeAsync));

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(
        IConfigurationAdministration administration,
        RequestSession browser,
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(browser);

        if (Known(key) is not ConfigurationKey known)
        {
            return Answers.Malformed("key");
        }

        return Answers.Of(
            await administration
                .ReadAsync(AccessContext.Of(browser.Required.Subject), known, cancellationToken)
                .ConfigureAwait(false),
            setting => TypedResults.Json(
                ConfiguredSettingView.Of(setting),
                ConfigurationJson.Default.ConfiguredSettingView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ChangeAsync(
        ConfigurationBody body,
        IConfigurationAdministration administration,
        RequestSession browser,
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(browser);

        if (Known(key) is not ConfigurationKey known)
        {
            return Answers.Malformed("key");
        }

        if (body.Value is not JsonElement value)
        {
            return Answers.Malformed("value");
        }

        // A missing reason is refused with the code chapter 09 section 8 names for it,
        // naming the key as the service names it, rather than as a malformed request.
        if (body.Reason is not { Length: > 0 } reason)
        {
            return Answers.Refused(Error.From(
                ErrorCodes.RestrictionReasonRequired,
                "key",
                JsonSerializer.SerializeToElement(known.ToString())));
        }

        return Answers.Of(
            await administration
                .ChangeAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    known,
                    value,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // The catalogue is what names a key; a name outside it, or outside the key format,
    // is not a key this interface knows.
    private static ConfigurationKey? Known(string key) =>
        Settings.All
            .FirstOrDefault(setting => string.Equals(setting.Key.ToString(), key, StringComparison.Ordinal))?
            .Key;
}
