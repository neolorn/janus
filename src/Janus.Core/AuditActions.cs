namespace Janus.Core;

/// <summary>
/// The audit actions of chapter 10 section 5 that the library records. The set is
/// closed: the library writes one of these and nothing else, and a member is added
/// here before it is written anywhere.
/// </summary>
/// <remarks>Implements IDN-AUD-001, CONV-NAME-003, PRIV-RET-004.</remarks>
public static class AuditActions
{
    /// <summary>
    /// A permission was refused, which is the row the refusal's correlation identifier resolves to.
    /// </summary>
    /// <remarks>Implements AUTHZ-CONCEAL-004, chapter 10 section 5.</remarks>
    public static AuditAction AccessDenied { get; } = AuditAction.Parse("authz.access.denied");

    /// <summary>
    /// An account was deactivated by its own owner.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-013, chapter 10 section 5.</remarks>
    public static AuditAction AccountDeactivated { get; } = AuditAction.Parse("identity.account.deactivated");

    /// <summary>
    /// A deactivated account was stood back up.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-013, chapter 10 section 5.</remarks>
    public static AuditAction AccountReactivated { get; } = AuditAction.Parse("identity.account.reactivated");

    /// <summary>
    /// The bot defence answered a send with a signal, which is recorded without the signal's own detail.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-009, chapter 10 section 5.</remarks>
    public static AuditAction BotDefenceSignalled { get; } = AuditAction.Parse("auth.botdefence.signalled");

    /// <summary>
    /// A configuration key was changed, with the key, the old value and the new one.
    /// </summary>
    /// <remarks>Implements OPS-CFG-005, chapter 10 section 5.</remarks>
    public static AuditAction ConfigurationChanged { get; } = AuditAction.Parse("ops.configuration.changed");

    /// <summary>
    /// A consent was granted for a purpose, naming the document version it was given against.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-004, chapter 10 section 5.</remarks>
    public static AuditAction ConsentGranted { get; } = AuditAction.Parse("privacy.consent.granted");

    /// <summary>
    /// A consent was withdrawn for a purpose.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-008, chapter 10 section 5.</remarks>
    public static AuditAction ConsentWithdrawn { get; } = AuditAction.Parse("privacy.consent.withdrawn");

    /// <summary>
    /// An authenticator presented a signature counter that did not advance, which is what a cloned credential looks like.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-002, chapter 10 section 5.</remarks>
    public static AuditAction CredentialCounterMismatch { get; } = AuditAction.Parse("auth.credential.countermismatch");

    /// <summary>
    /// A credential was enrolled on an account.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 5.</remarks>
    public static AuditAction CredentialEnrolled { get; } = AuditAction.Parse("auth.credential.enrolled");

    /// <summary>
    /// A credential was invalidated by a loss report that took effect.
    /// </summary>
    /// <remarks>Implements AUTH-REC-004, chapter 10 section 5.</remarks>
    public static AuditAction CredentialInvalidated { get; } = AuditAction.Parse("auth.credential.invalidated");

    /// <summary>
    /// An invalidation was held rather than carried out, because carrying it out would leave the account with no way in.
    /// </summary>
    /// <remarks>Implements AUTH-REC-004, chapter 10 section 5.</remarks>
    public static AuditAction CredentialInvalidationHeld { get; } = AuditAction.Parse("auth.credential.invalidationheld");

    /// <summary>
    /// A credential was given or renamed a label by its holder.
    /// </summary>
    /// <remarks>Implements REG-PM-002, chapter 10 section 5.</remarks>
    public static AuditAction CredentialLabelled { get; } = AuditAction.Parse("identity.credential.labelled");

    /// <summary>
    /// A credential was removed from an account.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 5.</remarks>
    public static AuditAction CredentialRemoved { get; } = AuditAction.Parse("auth.credential.removed");

    /// <summary>
    /// A loss report was cancelled before it took effect.
    /// </summary>
    /// <remarks>Implements AUTH-REC-004, chapter 10 section 5.</remarks>
    public static AuditAction CredentialReportCancelled { get; } = AuditAction.Parse("auth.credential.reportcancelled");

    /// <summary>
    /// A credential was reported lost, which starts the window before it is invalidated.
    /// </summary>
    /// <remarks>Implements AUTH-REC-004, chapter 10 section 5.</remarks>
    public static AuditAction CredentialReportedLost { get; } = AuditAction.Parse("auth.credential.reportedlost");

    /// <summary>
    /// A deletion was cancelled inside its grace window.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-014, chapter 10 section 5.</remarks>
    public static AuditAction DeletionCancelled { get; } = AuditAction.Parse("identity.deletion.cancelled");

    /// <summary>
    /// A deletion was requested, which opens the grace window it can be brought back from.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-014, chapter 10 section 5.</remarks>
    public static AuditAction DeletionRequested { get; } = AuditAction.Parse("identity.deletion.requested");

    /// <summary>
    /// An organization's deletion grace window elapsed and the erasure executed,
    /// which ends the memberships of it and leaves the row resolving.
    /// </summary>
    /// <remarks>Implements IDN-ORG-003, chapter 10 section 5.</remarks>
    public static AuditAction OrganizationErased { get; } = AuditAction.Parse("identity.organization.erased");

    /// <summary>
    /// An organization was created, with no override of the system policy.
    /// </summary>
    /// <remarks>Implements IDN-ORG-002 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationCreated { get; } = AuditAction.Parse("identity.organization.created");

    /// <summary>
    /// An organization's deletion was requested, which suspended it and opened the
    /// grace window.
    /// </summary>
    /// <remarks>Implements IDN-ORG-003 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationDeletionRequested { get; } =
        AuditAction.Parse("identity.organization.deletionrequested");

    /// <summary>
    /// An organization's deletion was cancelled inside its grace window, which lifted
    /// the suspension.
    /// </summary>
    /// <remarks>Implements IDN-ORG-003 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationDeletionCancelled { get; } =
        AuditAction.Parse("identity.organization.deletioncancelled");

    /// <summary>
    /// A domain was added to an organization's lock, unverified and admitting nothing.
    /// </summary>
    /// <remarks>Implements REG-DOM-001 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationDomainAdded { get; } =
        AuditAction.Parse("identity.organization.domainadded");

    /// <summary>
    /// A domain of an organization's lock was verified by its TXT record, and admits
    /// addresses in it from then on.
    /// </summary>
    /// <remarks>Implements REG-DOM-001 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationDomainVerified { get; } =
        AuditAction.Parse("identity.organization.domainverified");

    /// <summary>
    /// A domain was removed from an organization's lock, which stopped new sign-ins
    /// with addresses in it.
    /// </summary>
    /// <remarks>Implements REG-DOM-001 and IDN-AUD-001.</remarks>
    public static AuditAction OrganizationDomainRemoved { get; } =
        AuditAction.Parse("identity.organization.domainremoved");

    /// <summary>
    /// A takedown was triggered, naming what raised it and the reason written for it.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-003, chapter 10 section 5.</remarks>
    public static AuditAction TakedownExecuted { get; } = AuditAction.Parse("identity.takedown.executed");

    /// <summary>
    /// A takedown was reversed inside its window, with the reason written for it.
    /// </summary>
    /// <remarks>Implements IDN-LIFE-003, chapter 10 section 5.</remarks>
    public static AuditAction TakedownReversed { get; } = AuditAction.Parse("identity.takedown.reversed");

    /// <summary>
    /// A version of a legal document was published in the governing language.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-005, chapter 10 section 5.</remarks>
    public static AuditAction DocumentPublished { get; } = AuditAction.Parse("privacy.document.published");

    /// <summary>
    /// A translation was filed against a published version of a legal document.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-005, chapter 10 section 5.</remarks>
    public static AuditAction DocumentTranslated { get; } = AuditAction.Parse("privacy.document.translated");

    /// <summary>
    /// An erasure was carried out, which destroys the subject key and leaves the trail resolving.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-005, chapter 10 section 5.</remarks>
    public static AuditAction ErasureExecuted { get; } = AuditAction.Parse("privacy.erasure.executed");

    /// <summary>
    /// A subject export was assembled and made available to the subject.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-003, chapter 10 section 5.</remarks>
    public static AuditAction ExportAssembled { get; } = AuditAction.Parse("privacy.export.assembled");

    /// <summary>
    /// A group was created.
    /// </summary>
    /// <remarks>Implements AUTHZ-GROUP-001.</remarks>
    public static AuditAction GroupCreated { get; } = AuditAction.Parse("authz.group.created");

    /// <summary>
    /// An account or a group was added to a group, and holds what it holds.
    /// </summary>
    /// <remarks>Implements AUTHZ-GROUP-001 and OPS-CFG-007.</remarks>
    public static AuditAction GroupMemberAdded { get; } = AuditAction.Parse("authz.group.memberadded");

    /// <summary>
    /// An account or a group was taken out of a group, and no longer holds what it holds.
    /// </summary>
    /// <remarks>Implements AUTHZ-GROUP-001 and OPS-CFG-007.</remarks>
    public static AuditAction GroupMemberRemoved { get; } = AuditAction.Parse("authz.group.memberremoved");

    /// <summary>
    /// A group nothing named was removed.
    /// </summary>
    /// <remarks>Implements AUTHZ-GROUP-001.</remarks>
    public static AuditAction GroupRemoved { get; } = AuditAction.Parse("authz.group.removed");

    /// <summary>
    /// An objection to a purpose was recorded.
    /// </summary>
    /// <remarks>Implements PRIV-BASIS-003, chapter 10 section 5.</remarks>
    public static AuditAction ObjectionRecorded { get; } = AuditAction.Parse("privacy.objection.recorded");

    /// <summary>
    /// An objection to a purpose was withdrawn and the purpose resumed.
    /// </summary>
    /// <remarks>Implements PRIV-BASIS-003, chapter 10 section 5.</remarks>
    public static AuditAction ObjectionWithdrawn { get; } = AuditAction.Parse("privacy.objection.withdrawn");

    /// <summary>
    /// A phone signal was consulted before a send, recorded without the number it was consulted for.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-006, chapter 10 section 5.</remarks>
    public static AuditAction PhoneSignalConsidered { get; } = AuditAction.Parse("auth.phonesignal.considered");

    /// <summary>
    /// The account's preference values were changed, recorded by key and never by value.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 5.</remarks>
    public static AuditAction PreferencesChanged { get; } = AuditAction.Parse("identity.preferences.changed");

    /// <summary>
    /// A profile attribute of the account was changed.
    /// </summary>
    /// <remarks>Implements IDN-ATTR-001, chapter 10 section 5.</remarks>
    public static AuditAction ProfileChanged { get; } = AuditAction.Parse("identity.profile.changed");

    /// <summary>
    /// An assisted recovery was approved, naming the approver and the reason given.
    /// </summary>
    /// <remarks>Implements AUTH-REC-006, chapter 10 section 5.</remarks>
    public static AuditAction RecoveryApproved { get; } = AuditAction.Parse("auth.recovery.approved");

    /// <summary>
    /// A refresh token was presented a second time, which revokes the family it belongs to.
    /// </summary>
    /// <remarks>Implements AUTH-TOK-004, chapter 10 section 5.</remarks>
    public static AuditAction RefreshTokenReused { get; } = AuditAction.Parse("auth.oidc.refreshreused");

    /// <summary>
    /// A data subject request entered the queue staff work.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-002, chapter 10 section 5.</remarks>
    public static AuditAction RequestEntered { get; } = AuditAction.Parse("privacy.request.entered");

    /// <summary>
    /// A data subject request was fulfilled.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-002, chapter 10 section 5.</remarks>
    public static AuditAction RequestFulfilled { get; } = AuditAction.Parse("privacy.request.fulfilled");

    /// <summary>
    /// A data subject request reached its deadline undecided.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-002, chapter 10 section 5.</remarks>
    public static AuditAction RequestLapsed { get; } = AuditAction.Parse("privacy.request.lapsed");

    /// <summary>
    /// A data subject request was refused, with the reason recorded against it.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-002, chapter 10 section 5.</remarks>
    public static AuditAction RequestRefused { get; } = AuditAction.Parse("privacy.request.refused");

    /// <summary>
    /// A data subject request was submitted by the subject.
    /// </summary>
    /// <remarks>Implements PRIV-RIGHT-002, chapter 10 section 5.</remarks>
    public static AuditAction RequestSubmitted { get; } = AuditAction.Parse("privacy.request.submitted");

    /// <summary>
    /// A sending restriction was edited.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-005, chapter 10 section 5.</remarks>
    public static AuditAction RestrictionEdited { get; } = AuditAction.Parse("auth.restriction.edited");

    /// <summary>
    /// A sending restriction was granted against an address or a number.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-005, chapter 10 section 5.</remarks>
    public static AuditAction RestrictionGranted { get; } = AuditAction.Parse("auth.restriction.granted");

    /// <summary>
    /// A role was created, or the permissions it bundles were changed.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-004 and OPS-CFG-007.</remarks>
    public static AuditAction RoleDefined { get; } = AuditAction.Parse("authz.role.defined");

    /// <summary>
    /// A role nothing named was removed.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-004 and OPS-CFG-007.</remarks>
    public static AuditAction RoleRemoved { get; } = AuditAction.Parse("authz.role.removed");

    /// <summary>
    /// The account's preferred second step was changed.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-007, chapter 10 section 5.</remarks>
    public static AuditAction SecondStepPreferred { get; } = AuditAction.Parse("identity.secondstep.preferred");

    /// <summary>
    /// A session was presented, which is what a sign-in history is read from.
    /// </summary>
    /// <remarks>Implements AUTH-SESS-010, chapter 10 section 5.</remarks>
    public static AuditAction SessionPresented { get; } = AuditAction.Parse("auth.session.presented");

    /// <summary>
    /// The account's username was changed, which holds the old one for as long as the retention says.
    /// </summary>
    /// <remarks>Implements REG-IDENT-009, chapter 10 section 5.</remarks>
    public static AuditAction UsernameChanged { get; } = AuditAction.Parse("identity.username.changed");
}
