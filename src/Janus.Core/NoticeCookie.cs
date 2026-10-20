using System;

namespace Janus.Core;

/// <summary>
/// One cookie a version of the privacy notice lists: one the library sets or one the
/// host declared, as it stood when the version was published.
/// </summary>
/// <param name="Name">What the cookie is called.</param>
/// <param name="Purpose">
/// The consent-based purpose it serves, or null where it is strictly necessary.
/// </param>
/// <param name="Lifetime">How long it is set for, or null where it ends with the browser's session.</param>
/// <param name="ThirdParty">Whether any third party can read it.</param>
/// <remarks>
/// Implements PRIV-CONS-006a and chapter 09 <c>GET /privacy/notice</c> (D-203). The list
/// belongs to the version, so a reader of an old version sees what it listed.
/// </remarks>
public sealed record NoticeCookie(
    string Name,
    string? Purpose,
    TimeSpan? Lifetime,
    bool ThirdParty);
