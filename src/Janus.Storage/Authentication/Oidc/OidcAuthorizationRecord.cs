using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_authorizations</c> row: one grant a person made to one client, which
/// the codes and tokens issued under it hang from.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001 and AUTH-OIDC-003. The status and the type are the
/// protocol server's vocabulary and the library writes neither, so they are held as
/// what the server put there; the subject and the client are the deployment's own
/// rows, so they are held as those and go when those go.
/// </remarks>
internal sealed class OidcAuthorizationRecord
{
    /// <summary>The <c>id</c> column: which grant.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>concurrency_token</c> column: what a write is refused for not holding,
    /// so two uses of one row cannot both succeed.
    /// </summary>
    public Guid ConcurrencyToken { get; set; }

    /// <summary>The <c>application_id</c> column: which client holds it.</summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>The <c>subject</c> column: whose account it is for.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>status</c> column: what the server last made of it.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The <c>type</c> column: whether it stands beyond one exchange.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The <c>scopes</c> column: what it covers.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>The <c>properties</c> column: what a caller attached to it.</summary>
    public string Properties { get; set; } = "{}";
}
