using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Authentication.Accounts;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Invitations;
using Janus.Authentication.Oidc;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
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
            typeof(HeldSigningKey),
            typeof(SigningKeySet),
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
            Member<GeneratedBreakGlass>(nameof(GeneratedBreakGlass.Credential)),
        ]));

    /// <summary>
    /// CONV-LOG-003 AC1: the correlation reference authenticates an unsigned callback,
    /// so it is a token: its type, the member a delivery report carries it in, and
    /// every parameter it passes through before it is hashed are marked.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryCorrelationReferenceIsMarked() =>
        Assert.Empty(UnmarkedCarriers(
        [
            typeof(SendReference),
            Member<SmsDeliveryReport>(nameof(SmsDeliveryReport.Reference)),
            Parameter(typeof(DeliveryReports), nameof(DeliveryReports.ReportAsync), "reference"),
            Parameter(typeof(SendReferences), nameof(SendReferences.Of), "presented"),
            Parameter(typeof(CallbackReferences), nameof(CallbackReferences.RecognisesAsync), "presented"),
            Parameter(typeof(CallbackReferences), "Hashed", "reference"),
        ]));

    private static PropertyInfo Member<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no member {name}.");

    // The one parameter of that name among the methods of that name, so an overload
    // that takes no such parameter is passed over.
    private static ParameterInfo Parameter(Type type, string method, string name) =>
        type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(candidate => candidate.Name == method)
            .SelectMany(candidate => candidate.GetParameters())
            .Single(parameter => parameter.Name == name);

    private static IEnumerable<string> Unmarked(IEnumerable<MemberInfo> carriers) =>
        carriers
            .Where(carrier => !carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false))
            .Select(carrier => carrier is Type type ? type.Name : $"{carrier.DeclaringType!.Name}.{carrier.Name}");

    private static IEnumerable<string> UnmarkedCarriers(IReadOnlyList<object> carriers) =>
        Unmarked(carriers.OfType<MemberInfo>())
            .Concat(carriers
                .OfType<ParameterInfo>()
                .Where(carrier => !carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false))
                .Select(carrier => $"{carrier.Member.DeclaringType!.Name}.{carrier.Member.Name}({carrier.Name})"));
}
