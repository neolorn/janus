using System;

namespace Janus.Core;

/// <summary>
/// One cookie the host sets, which the privacy notice lists beside the library's own.
/// </summary>
/// <param name="Name">What the cookie is called.</param>
/// <param name="Lifetime">How long it is set for, or null where it ends with the browser's session.</param>
/// <param name="ThirdPartyReadable">Whether any third party can read it.</param>
/// <param name="Purpose">
/// The consent-based purpose it serves, or null where it is strictly necessary.
/// </param>
/// <remarks>
/// Implements PRIV-CONS-006a and LIB-HOST-001 (D-203). A purpose not declared or not
/// resting on consent, a name the library's own cookies take, a name that is no cookie
/// name, or a cookie declared twice stops the deployment at startup.
/// </remarks>
public sealed record CookieDeclaration(
    string Name,
    TimeSpan? Lifetime,
    bool ThirdPartyReadable,
    string? Purpose);
