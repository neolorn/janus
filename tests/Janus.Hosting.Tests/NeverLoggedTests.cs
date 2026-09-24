using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Core;
using Janus.Hosting.Accounts;
using Janus.Hosting.Authentication;
using Janus.Hosting.Bff;
using Janus.Hosting.BreakGlass;
using Janus.Hosting.Credentials;
using Janus.Hosting.Organizations;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// That every carrier of a value CONV-LOG-003 forbids at the HTTP boundary is marked,
/// so rule JAN0002 refuses a logging call that takes one (CONV-LOG-003,
/// CONV-CODE-008).
/// </summary>
[Trait("kind", "contract")]
public sealed class NeverLoggedTests
{
    /// <summary>
    /// CONV-LOG-003 AC1: the sign-on secret and the recovery codes shown once are
    /// marked as types.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryTypeCarryingAForbiddenValueIsMarked() =>
        Assert.Empty(Unmarked([typeof(SignOnSecret), typeof(RecoveryCodesView)]));

    /// <summary>
    /// CONV-LOG-003 AC1: every password, code and token a request carries in or an
    /// answer carries out is marked where it is a member.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryMemberCarryingAForbiddenValueIsMarked() =>
        Assert.Empty(Unmarked(
        [
            Member<SetPasswordRequest>(nameof(SetPasswordRequest.Password)),
            Member<SecurityRequest>(nameof(SecurityRequest.Password)),
            Member<CompleteRecoveryRequest>(nameof(CompleteRecoveryRequest.Password)),
            Member<CompleteRecoveryRequest>(nameof(CompleteRecoveryRequest.Token)),
            Member<CancelLossRequest>(nameof(CancelLossRequest.Token)),
            Member<EnrolmentRequest>(nameof(EnrolmentRequest.Token)),
            Member<BeginRegistrationRequest>(nameof(BeginRegistrationRequest.InvitationToken)),
            Member<PresentFactorRequest>(nameof(PresentFactorRequest.Value)),
            Member<PresentFactorRequest>(nameof(PresentFactorRequest.LinkToken)),
            Member<VerifyRequest>(nameof(VerifyRequest.Code)),
            Member<VerifyRequest>(nameof(VerifyRequest.LinkToken)),
            Member<VerifyIdentifierRequest>(nameof(VerifyIdentifierRequest.Code)),
            Member<VerifyIdentifierRequest>(nameof(VerifyIdentifierRequest.LinkToken)),
            Member<VerifyDeviceRequest>(nameof(VerifyDeviceRequest.Code)),
            Member<ConfirmGeneratorRequest>(nameof(ConfirmGeneratorRequest.Code)),
            Member<LinkTokenRequest>(nameof(LinkTokenRequest.LinkToken)),
            Member<AbandonLinkRequest>(nameof(AbandonLinkRequest.LinkToken)),
            Member<AbandonRequest>(nameof(AbandonRequest.LinkToken)),
            Member<LinkLandingView>(nameof(LinkLandingView.Code)),
            Member<SignInLandingView>(nameof(SignInLandingView.Code)),
            Member<IdentifierLandingView>(nameof(IdentifierLandingView.Code)),
            Member<GeneratorEnrolmentView>(nameof(GeneratorEnrolmentView.Secret)),
            Member<GeneratorEnrolmentView>(nameof(GeneratorEnrolmentView.Uri)),
            Member<IssuedInvitationView>(nameof(IssuedInvitationView.Token)),
            Member<IssuedAppPasswordView>(nameof(IssuedAppPasswordView.Secret)),
            Member<PresentBreakGlassRequest>(nameof(PresentBreakGlassRequest.Credential)),
            Member<GeneratedBreakGlassView>(nameof(GeneratedBreakGlassView.Credential)),
        ]));

    private static PropertyInfo Member<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no member {name}.");

    private static IEnumerable<string> Unmarked(IEnumerable<MemberInfo> carriers) =>
        carriers
            .Where(carrier => !carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false))
            .Select(carrier => carrier is Type type ? type.Name : $"{carrier.DeclaringType!.Name}.{carrier.Name}");
}
