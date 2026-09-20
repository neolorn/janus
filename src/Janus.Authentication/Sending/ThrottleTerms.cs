using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// The configured shape of the progressive delay, read once for an attempt rather
/// than once per scope.
/// </summary>
/// <param name="Enabled">Whether the control is in force at all.</param>
/// <param name="Threshold">The failures that earn no delay.</param>
/// <param name="Initial">The first delay after them.</param>
/// <param name="Factor">What each further failure multiplies it by.</param>
/// <param name="Maximum">The per-source ceiling.</param>
/// <param name="AccountCap">The cap the account and identifier components share.</param>
/// <param name="Decay">How long the accumulation takes to halve.</param>
/// <remarks>Implements AUTH-ABUSE-001 and chapter 10 section 4.5.</remarks>
internal sealed record ThrottleTerms(
    bool Enabled,
    int Threshold,
    TimeSpan Initial,
    decimal Factor,
    TimeSpan Maximum,
    TimeSpan AccountCap,
    TimeSpan Decay);
