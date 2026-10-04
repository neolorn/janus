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
    /// The action is refused until the record's data subject consents again, the consent
    /// held having been superseded (<c>privacy.consent.superseded</c>).
    /// </summary>
    ConsentSuperseded,

    /// <summary>
    /// The action is refused until the record's data subject gives the written consent
    /// the purpose asks (<c>privacy.consent.writtenrequired</c>).
    /// </summary>
    ConsentWrittenRequired,

    /// <summary>
    /// The action is refused until the caller proves what the gate bound to it costs
    /// (<c>auth.stepup.required</c>).
    /// </summary>
    StepUpRequired,

    /// <summary>
    /// The action is refused for step-up, and its capability asks the caller to
    /// authenticate again: the session would meet the gate but for proof attained before
    /// its last downgrade (<c>reauthenticate</c>).
    /// </summary>
    ReauthenticationRequired,

    /// <summary>
    /// The action is refused where nothing reports what the caller proved
    /// (<c>auth.stepup.unavailable</c>).
    /// </summary>
    StepUpUnavailable,

    /// <summary>
    /// The question is the calling code's fault, raised before anything is read.
    /// </summary>
    Raised,
}
