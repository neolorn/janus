using System;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// What a session record a token is minted from carries: whose it is, how long the
/// access token may last, and the instant nothing standing on the record outlives.
/// </summary>
/// <param name="Subject">Whose account the record is.</param>
/// <param name="AccessTokenLifetime">
/// How long an access token minted now may last, which is
/// <c>oidc.accesstoken.lifetime</c>.
/// </param>
/// <param name="Remaining">
/// How long the record still has, which is the earlier of its idle and its absolute
/// expiry less now, and which a refresh token never outlives.
/// </param>
/// <remarks>
/// Implements AUTH-OIDC-003 and AUTH-OIDC-004. A refresh token is a handle on the
/// record and not a credential of its own, so there is no lifetime of its own to set:
/// it stops when the record does.
/// </remarks>
internal sealed record MintedSession(
    SubjectId Subject,
    TimeSpan AccessTokenLifetime,
    TimeSpan Remaining);
