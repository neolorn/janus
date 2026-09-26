namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a truth-table case decides, as the path that is asked answers it.
/// </summary>
public enum Decided
{
    /// <summary>
    /// The action is admitted.
    /// </summary>
    Allowed,

    /// <summary>
    /// The action is refused, as nothing the caller holds confers it (<c>authz.denied</c>).
    /// </summary>
    Denied,

    /// <summary>
    /// The action is refused while the caller's account is restricted (<c>authz.restricted</c>).
    /// </summary>
    Restricted,

    /// <summary>
    /// The action is refused until the record's data subject consents to the purpose it
    /// serves (<c>privacy.consent.required</c>).
    /// </summary>
    ConsentRequired,

    /// <summary>
    /// The question is the calling code's fault, raised before anything is read.
    /// </summary>
    Raised,
}
