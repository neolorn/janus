using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What presenting a factor reached.
/// </summary>
/// <param name="Status">Where the sign-in stands.</param>
/// <param name="AssuranceLevel">The level the session has reached.</param>
/// <param name="PhishingResistant">Whether what was presented resists phishing.</param>
/// <param name="Required">
/// What is still wanted, populated only where the status is
/// <see cref="SignInStatus.FactorRequired"/>.
/// </param>
/// <param name="TrustDeviceOffered">Whether the policy permits trusting this browser.</param>
/// <param name="Session">The session, where one now exists.</param>
/// <param name="Requirement">
/// The raised policy requirement and the instant its grace ends, where the account
/// does not yet meet one.
/// </param>
/// <param name="PasswordChangeRequired">
/// Whether the password has to be changed before anything else, which a password now
/// on the blocklist or below its floor sets.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-016, AUTH-FACT-017 and AUTH-SESS-002. The
/// properties reached are reported, never which factor produced them (`09` section 3).
/// </remarks>
public sealed record SignInProgress(
    SignInStatus Status,
    AssuranceLevel AssuranceLevel,
    bool PhishingResistant,
    IReadOnlyList<Factor> Required,
    bool TrustDeviceOffered,
    SessionId? Session,
    PolicyRequirement? Requirement,
    bool PasswordChangeRequired);
