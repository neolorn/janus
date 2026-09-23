using System;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_scopes</c> row: one scope the deployment registered beyond the ones the
/// provider is built with.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001 and CONV-CONTENT-001. The wording columns are the
/// deployment's: the library writes no sentence into them and reads none, and what it
/// serves from the table is the name and the resources.
/// </remarks>
internal sealed class OidcScopeRecord
{
    /// <summary>The <c>id</c> column: which scope.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>name</c> column: what a request asks for it by.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>display_name</c> column, as the deployment wrote it.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The <c>display_names</c> column, by culture.</summary>
    public string DisplayNames { get; set; } = "{}";

    /// <summary>The <c>description</c> column, as the deployment wrote it.</summary>
    public string? Description { get; set; }

    /// <summary>The <c>descriptions</c> column, by culture.</summary>
    public string Descriptions { get; set; } = "{}";

    /// <summary>The <c>resources</c> column: what a token covering it is good for.</summary>
    public string[] Resources { get; set; } = [];

    /// <summary>The <c>properties</c> column: what a caller attached to it.</summary>
    public string Properties { get; set; } = "{}";
}
