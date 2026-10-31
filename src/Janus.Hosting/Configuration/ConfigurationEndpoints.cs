using System;
using System.Linq;
using System.Net.Mime;
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
/// The runtime configuration endpoints of chapter 09 section 8, reading one key and
/// changing it, and <c>GET /settings</c>, the settings a frontend renders from.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-002, OPS-CFG-004, LIB-API-005, CONV-CODE-006, CONV-DESIGN-006
/// and D-224. The two of section 8 are each one line to
/// <see cref="IConfigurationAdministration"/>, which judges the permission, the
/// direction and the value; a body missing a member it requires is refused before it
/// is called. The settings are not an operation and are read by
/// <see cref="FrontendSettings"/>.
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

        _ = SessionRequired.On(endpoints.MapGet("/admin/config/{key}", ReadAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied)
                .Binding<ConfigurationKey>("key"))
            .Produces<ConfiguredSettingView>();
        _ = SessionRequired.On(endpoints.MapPut("/admin/config/{key}", ChangeAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired,
                    ErrorCodes.ConfigurationValueBelowFloor, ErrorCodes.ConfigurationValueAboveCeiling,
                    ErrorCodes.ConfigurationValueNotAllowed, ErrorCodes.ConfigurationKeyProtected,
                    ErrorCodes.EndpointInsecure,
                    ErrorCodes.ConfigurationChangeReasonRequired)
                .Binding<ConfigurationKey>("key"))
            .Accepts<ConfigurationBody>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = endpoints.MapGet("/settings", ReadSettingsAsync)
            .Declares(EndpointDeclaration.Answering())
            .Produces<FrontendSettingsView>();

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(
        IConfigurationAdministration administration,
        AuthorizationDeclaration declaration,
        RequestSession browser,
        ConfigurationKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(browser);

        if (Known(key, declaration) is not ConfigurationKey known)
        {
            return Answers.Malformed("key");
        }

        return Answers.Of(
            await administration
                .ReadAsync(browser.Asking, known, cancellationToken)
                .ConfigureAwait(false),
            setting => TypedResults.Json(
                ConfiguredSettingView.Of(setting),
                ConfigurationJson.Default.ConfiguredSettingView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // D-224: what the screens render from is read afresh on each request, so a change
    // to a key or to a policy reaches the next screen and no cache holds the old one.
    private static async Task<IResult> ReadSettingsAsync(
        FrontendSettings settings,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.CacheControl = "no-store";

        return TypedResults.Json(
            await settings.ReadAsync(browser, cancellationToken).ConfigureAwait(false),
            ConfigurationJson.Default.FrontendSettingsView,
            contentType: null,
            StatusCodes.Status200OK);
    }

    private static async Task<IResult> ChangeAsync(
        IConfigurationAdministration administration,
        AuthorizationDeclaration declaration,
        RequestSession browser,
        ConfigurationKey key,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        ConfigurationBody body = await JsonBody
            .ReadAsync<ConfigurationBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Known(key, declaration) is not ConfigurationKey known)
        {
            return Answers.Malformed("key");
        }

        if (body.Value is not JsonElement value)
        {
            return Answers.Malformed("value");
        }

        // A missing or blank reason is refused with the code chapter 09 section 8 names
        // for it, naming the key as the service names it, rather than as a malformed
        // request; one past the bound of API-CONV-002 is a request the boundary does not
        // read.
        if (body.Reason?.Trim() is not { Length: > 0 } reason)
        {
            return Answers.Refused(Error.From(
                ErrorCodes.ConfigurationChangeReasonRequired,
                "key",
                JsonSerializer.SerializeToElement(known.ToString())));
        }

        if (reason.Length > 1024)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await administration
                .ChangeAsync(
                    browser.Asking,
                    browser.Required.Id,
                    known,
                    value,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // The catalogue and the categories the host declared are what name a key; a
    // well-formed name outside them is not a key this interface knows.
    private static ConfigurationKey? Known(ConfigurationKey key, AuthorizationDeclaration declaration) =>
        Settings.All
            .Select(setting => setting.Key)
            .Concat(declaration.RetentionFloors.Keys.Select(Settings.HostCategoryRetention.For))
            .Where(known => known == key)
            .Select(known => (ConfigurationKey?)known)
            .FirstOrDefault();
}
