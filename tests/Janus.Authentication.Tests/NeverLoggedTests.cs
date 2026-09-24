using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Invitations;
using Janus.Authentication.Oidc;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests;

/// <summary>
/// That every carrier of a value CONV-LOG-003 forbids in authentication is marked, so
/// rule JAN0002 refuses a logging call that takes one (CONV-LOG-003, CONV-CODE-008).
/// </summary>
[Trait("kind", "contract")]
public sealed class NeverLoggedTests
{
    /// <summary>
    /// CONV-LOG-003 AC1: passwords and their hashes, tokens, TOTP secrets, recovery
    /// and verification codes, and the signing key are marked as types.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryTypeCarryingAForbiddenValueIsMarked() =>
        Assert.Empty(Unmarked(
        [
            typeof(Password),
            typeof(PasswordHash),
            typeof(OpaqueToken),
            typeof(TotpMaterial),
            typeof(TotpEnrolment),
            typeof(VerificationCode),
            typeof(RecoveryCodeEntry),
            typeof(PreparedRecoveryCodes),
            typeof(SigningMaterial),
        ]));

    /// <summary>
    /// CONV-LOG-003 AC1: where a forbidden value is one member of a record that is not
    /// secret as a whole, that member is marked.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryMemberCarryingAForbiddenValueIsMarked() =>
        Assert.Empty(Unmarked(
        [
            Member<LifecycleLink>(nameof(LifecycleLink.Token)),
            Member<Invitation>(nameof(Invitation.Token)),
            Member<LockedDomain>(nameof(LockedDomain.Token)),
            Member<LossReport>(nameof(LossReport.Cancel)),
            Member<RecoveryLink>(nameof(RecoveryLink.Fingerprint)),
            Member<PendingVerification>(nameof(PendingVerification.OldLink)),
            Member<StagedIdentity>(nameof(StagedIdentity.Code)),
            Member<StagedIdentity>(nameof(StagedIdentity.Link)),
            Member<PreAuthentication>(nameof(PreAuthentication.Fingerprint)),
            Member<PreAuthentication>(nameof(PreAuthentication.CsrfFingerprint)),
            Member<SignOnAttempt>(nameof(SignOnAttempt.Verifier)),
            Member<PendingSignIn>(nameof(PendingSignIn.Fingerprint)),
            Member<PendingSignIn>(nameof(PendingSignIn.Code)),
            Member<PendingSignIn>(nameof(PendingSignIn.Browser)),
            Member<LandedSignIn>(nameof(LandedSignIn.Code)),
        ]));

    private static PropertyInfo Member<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no member {name}.");

    private static IEnumerable<string> Unmarked(IEnumerable<MemberInfo> carriers) =>
        carriers
            .Where(carrier => !carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false))
            .Select(carrier => carrier is Type type ? type.Name : $"{carrier.DeclaringType!.Name}.{carrier.Name}");
}
