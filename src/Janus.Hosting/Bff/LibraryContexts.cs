using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Janus.Hosting.Accounts;
using Janus.Hosting.Authentication;
using Janus.Hosting.Authorization;
using Janus.Hosting.BreakGlass;
using Janus.Hosting.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Maintenance;
using Janus.Hosting.Organizations;
using Janus.Hosting.Privacy;
using Janus.Hosting.Records;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
using Janus.Hosting.Sending;

namespace Janus.Hosting.Bff;

/// <summary>
/// The library's serializer contexts, each under its own options, through which every
/// body of <c>09</c> is read and written.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006 and CONV-DESIGN-007 (D-206). None of them is added to the
/// application's JSON options, so no setting a host makes for its own endpoints
/// changes how a body of the library reads.
/// </remarks>
internal static class LibraryContexts
{
    /// <summary>
    /// Every context of the library.
    /// </summary>
    public static IReadOnlyList<JsonSerializerContext> All { get; } =
    [
        RegistrationJson.Default,
        RecordsJson.Default,
        AuthenticationJson.Default,
        BreakGlassJson.Default,
        MaintenanceJson.Default,
        AccountJson.Default,
        RecoveryJson.Default,
        CredentialsJson.Default,
        WellKnownJson.Default,
        PrivacyJson.Default,
        ConfigurationJson.Default,
        SendingJson.Default,
        AuthorizationJson.Default,
        OrganizationJson.Default,
        ApiErrorJson.Default,
    ];

    /// <summary>
    /// The first context that declares a type, which reads and writes it under its own
    /// options.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The context, or nothing where none declares the type.</returns>
    /// <exception cref="ArgumentNullException">The type is absent.</exception>
    public static JsonSerializerContext? Declaring(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        foreach (JsonSerializerContext context in All)
        {
            if (context.GetTypeInfo(type) is not null)
            {
                return context;
            }
        }

        return null;
    }
}
