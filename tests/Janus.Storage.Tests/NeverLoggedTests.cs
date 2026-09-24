using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Core;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authentication.Oidc;
using Janus.Storage.Authentication.Organizations;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Authentication.Recovery;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Authentication.SignIn;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// That every column holding a value CONV-LOG-003 forbids, or the fingerprint a
/// session or a link is found by, is marked, so rule JAN0002 refuses a logging call
/// that takes one (CONV-LOG-003, CONV-CODE-008).
/// </summary>
[Trait("kind", "contract")]
public sealed class NeverLoggedTests
{
    /// <summary>
    /// CONV-LOG-003 AC1: the rows' secret columns are marked.
    /// </summary>
    [Fact]
    public void CONV_LOG_003_AC1_EveryColumnCarryingAForbiddenValueIsMarked() =>
        Assert.Empty(Unmarked(
        [
            Member<PasswordRecord>(nameof(PasswordRecord.Hash)),
            Member<RecoveryCodeRecord>(nameof(RecoveryCodeRecord.Hash)),
            Member<VerificationCodeRecord>(nameof(VerificationCodeRecord.Code)),
            Member<LifecycleLinkRecord>(nameof(LifecycleLinkRecord.Token)),
            Member<RecoveryLinkRecord>(nameof(RecoveryLinkRecord.Token)),
            Member<InvitationRecord>(nameof(InvitationRecord.Token)),
            Member<LockedDomainRecord>(nameof(LockedDomainRecord.Token)),
            Member<LossReportRecord>(nameof(LossReportRecord.Cancel)),
            Member<PendingVerificationRecord>(nameof(PendingVerificationRecord.Link)),
            Member<PendingVerificationRecord>(nameof(PendingVerificationRecord.OldLink)),
            Member<PendingSignInRecord>(nameof(PendingSignInRecord.Token)),
            Member<PendingSignInRecord>(nameof(PendingSignInRecord.Code)),
            Member<RegistrationSessionRecord>(nameof(RegistrationSessionRecord.Session)),
            Member<SessionRecord>(nameof(SessionRecord.SecretFingerprint)),
            Member<SessionRecord>(nameof(SessionRecord.CsrfFingerprint)),
            Member<PreAuthenticationRecord>(nameof(PreAuthenticationRecord.Fingerprint)),
            Member<PreAuthenticationRecord>(nameof(PreAuthenticationRecord.CsrfFingerprint)),
            Member<PreAuthenticationRecord>(nameof(PreAuthenticationRecord.SignOnVerifier)),
            Member<OidcClientRecord>(nameof(OidcClientRecord.Secret)),
            Member<OidcTokenRecord>(nameof(OidcTokenRecord.Payload)),
            Member<SigningKeyRecord>(nameof(SigningKeyRecord.PrivateKey)),
            Member<StagedIdentityDocument>(nameof(StagedIdentityDocument.Code)),
            Member<StagedIdentityDocument>(nameof(StagedIdentityDocument.Link)),
            Member<StagedSessionDocument>(nameof(StagedSessionDocument.Password)),
        ]));

    private static PropertyInfo Member<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no member {name}.");

    private static IEnumerable<string> Unmarked(IEnumerable<MemberInfo> carriers) =>
        carriers
            .Where(carrier => !carrier.IsDefined(typeof(NeverLoggedAttribute), inherit: false))
            .Select(carrier => carrier is Type type ? type.Name : $"{carrier.DeclaringType!.Name}.{carrier.Name}");
}
