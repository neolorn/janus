using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// The permissions the library ships, because they govern its own endpoints and
/// operations. A host declares its own in the model builder and redefines none of
/// these.
/// </summary>
/// <remarks>Implements chapter 10 section 2.1, LIB-API-005.</remarks>
[SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "The conflicting namespace is a .NET Framework one this library never references, and chapter 10 section 2.1 names the catalogue the permissions.")]
public static class Permissions
{
    /// <summary>
    /// Viewing grants, the "who can access this?" view, and which accounts may exercise a
    /// permission.
    /// </summary>
    public static Permission GrantRead { get; } = Permission.Parse("grant:read");

    /// <summary>
    /// Creating and revoking grants.
    /// </summary>
    public static Permission GrantManage { get; } = Permission.Parse("grant:manage");

    /// <summary>
    /// Creating and changing roles.
    /// </summary>
    public static Permission RoleManage { get; } = Permission.Parse("role:manage");

    /// <summary>
    /// Creating groups, nesting them, and changing their members.
    /// </summary>
    public static Permission GroupManage { get; } = Permission.Parse("group:manage");

    /// <summary>
    /// Organization lifecycle, policy, and deletion cancellation.
    /// </summary>
    public static Permission OrganizationManage { get; } = Permission.Parse("organization:manage");

    /// <summary>
    /// Adding and removing members; issuing and revoking invitations, bound or open.
    /// </summary>
    public static Permission MembershipManage { get; } = Permission.Parse("membership:manage");

    /// <summary>
    /// Reading the organizations the deployment holds: in the administrative organization
    /// every one, elsewhere the organization itself. A host's job may declare it.
    /// </summary>
    public static Permission OrganizationRead { get; } = Permission.Parse("organization:read");

    /// <summary>
    /// Reading an organization's current members, one account's membership and the
    /// organization's standing invitations. A host's job may declare it.
    /// </summary>
    public static Permission MembershipRead { get; } = Permission.Parse("membership:read");

    /// <summary>
    /// Adding, verifying and removing an organization's locked email domains.
    /// </summary>
    public static Permission DomainManage { get; } = Permission.Parse("domain:manage");

    /// <summary>
    /// Reading and editing the named restriction set.
    /// </summary>
    public static Permission RestrictionEdit { get; } = Permission.Parse("restriction:edit");

    /// <summary>
    /// Granting credit to one key under a restriction.
    /// </summary>
    public static Permission RestrictionGrant { get; } = Permission.Parse("restriction:grant");

    /// <summary>
    /// Suspending and reactivating an account, lifting a restriction, cancelling a
    /// deletion on a subject's behalf, and setting another person's preferences.
    /// </summary>
    public static Permission AccountManage { get; } = Permission.Parse("account:manage");

    /// <summary>
    /// Reading another person's account and finding an account by an identifier; a
    /// person's alone.
    /// </summary>
    public static Permission AccountRead { get; } = Permission.Parse("account:read");

    /// <summary>
    /// Reading another person's display name, primary verified email and phone,
    /// language, time zone and state. A host's job may declare it.
    /// </summary>
    public static Permission ContactRead { get; } = Permission.Parse("contact:read");

    /// <summary>
    /// Reading another person's state, consents and objections. A host's job may
    /// declare it.
    /// </summary>
    public static Permission ConsentRead { get; } = Permission.Parse("consent:read");

    /// <summary>
    /// Entering a person record for someone who gave their details to staff, asked in
    /// the organization the record is entered in.
    /// </summary>
    public static Permission RecordCreate { get; } = Permission.Parse("record:create");

    /// <summary>
    /// Reading and finding person records and the records attached to an account. A
    /// host's job may declare it.
    /// </summary>
    public static Permission RecordRead { get; } = Permission.Parse("record:read");

    /// <summary>
    /// Correcting a person record, recording a consent or an objection the person gave
    /// staff, exporting a record for the person, lifting its restriction and cancelling
    /// its deletion.
    /// </summary>
    public static Permission RecordManage { get; } = Permission.Parse("record:manage");

    /// <summary>
    /// Erasing a person record the host keeps nothing about any longer. A host's job may
    /// declare it.
    /// </summary>
    public static Permission RecordErase { get; } = Permission.Parse("record:erase");

    /// <summary>
    /// The minor takedown operation.
    /// </summary>
    public static Permission TakedownExecute { get; } = Permission.Parse("takedown:execute");

    /// <summary>
    /// Administrative re-enrolment.
    /// </summary>
    public static Permission RecoveryApprove { get; } = Permission.Parse("recovery:approve");

    /// <summary>
    /// Emergency revocation of all sessions, system-wide.
    /// </summary>
    public static Permission SessionRevoke { get; } = Permission.Parse("session:revoke");

    /// <summary>
    /// Revoking one account's sessions, for offboarding or suspension, and listing them.
    /// </summary>
    public static Permission SessionRevokeAccount { get; } = Permission.Parse("session:revoke-account");

    /// <summary>
    /// Reading configuration.
    /// </summary>
    public static Permission ConfigurationRead { get; } = Permission.Parse("config:read");

    /// <summary>
    /// Changing configuration.
    /// </summary>
    public static Permission ConfigurationManage { get; } = Permission.Parse("config:manage");

    /// <summary>
    /// Reading the audit trail, querying it by subject, and resolving a concealed
    /// denial's correlation identifier.
    /// </summary>
    public static Permission AuditRead { get; } = Permission.Parse("audit:read");

    /// <summary>
    /// Generating records of processing.
    /// </summary>
    public static Permission RecordsOfProcessingRead { get; } = Permission.Parse("ropa:read");

    /// <summary>
    /// The data-subject request queue, erasure progress, and the manual completion
    /// path.
    /// </summary>
    public static Permission PrivacyRequestManage { get; } = Permission.Parse("privacyrequest:manage");

    /// <summary>
    /// Publishing a compliance-text version.
    /// </summary>
    public static Permission NoticePublish { get; } = Permission.Parse("notice:publish");

    /// <summary>
    /// Licence and permit dates, assessment references, and the declared human-input
    /// fields of the records of processing.
    /// </summary>
    public static Permission ComplianceManage { get; } = Permission.Parse("compliance:manage");

    /// <summary>
    /// Loosening configuration changes and granting the seeded administrative role.
    /// Granting or revoking it requires holding it.
    /// </summary>
    public static Permission SystemAdminister { get; } = Permission.Parse("system:administer");

    /// <summary>
    /// Every permission the library ships, in the order of the permission string,
    /// which a host may neither redefine nor extend.
    /// </summary>
    public static IReadOnlyList<Permission> All { get; } =
    [
        AccountManage,
        AccountRead,
        AuditRead,
        ComplianceManage,
        ConfigurationManage,
        ConfigurationRead,
        ConsentRead,
        ContactRead,
        DomainManage,
        GrantManage,
        GrantRead,
        GroupManage,
        MembershipManage,
        MembershipRead,
        NoticePublish,
        OrganizationManage,
        OrganizationRead,
        PrivacyRequestManage,
        RecordCreate,
        RecordErase,
        RecordManage,
        RecordRead,
        RecoveryApprove,
        RestrictionEdit,
        RestrictionGrant,
        RoleManage,
        RecordsOfProcessingRead,
        SessionRevoke,
        SessionRevokeAccount,
        SystemAdminister,
        TakedownExecute,
    ];
}
