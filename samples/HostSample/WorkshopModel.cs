using System;
using Janus.Core;

namespace HostSample;

/// <summary>
/// What the workshop declares to the library about its own domain: its kinds of record
/// and how they contain one another, what may be done to them and by whom, the facts its
/// roles follow from, what it holds about people and why, and the job it runs.
/// </summary>
internal static class WorkshopModel
{
    /// <summary>
    /// The relationship the repair-owner role follows from.
    /// </summary>
    public const string Owner = "owner";

    /// <summary>
    /// The relationship the repair-collector role follows from.
    /// </summary>
    public const string Collector = "collector";

    /// <summary>
    /// The purpose a repair's contact name is held for.
    /// </summary>
    public const string CarryingOutRepairs = "carrying out repairs";

    /// <summary>
    /// The purpose the workshop calls a customer after a repair for, which the customer
    /// may object to.
    /// </summary>
    public const string FollowingUpRepairs = "following up repairs";

    /// <summary>
    /// The purpose a grip fitting is made for, which rests on the customer's written
    /// consent.
    /// </summary>
    public const string AdaptingGrips = "adapting grips";

    /// <summary>
    /// The encrypted field holding the name a repair's customer is contacted by.
    /// </summary>
    public const string ContactName = "contact-name";

    /// <summary>
    /// The encrypted field holding what a customer's grip needs.
    /// </summary>
    public const string GripNeed = "grip-need";

    // The step-up gate releasing a kite is bound to, a gate of the workshop's own naming.
    private const string Releasing = "releasing";

    // What the workshop holds, and of whom.
    private const string Catalogue = "catalogue";

    private const string Contact = "contact";

    private const string Health = "health";

    private const string Customers = "customers";

    private const string Staff = "staff";

    // The workshop's record of the balancing test its follow-up calls rest on.
    private const string FollowUpAssessment = "workshop-assessment-follow-up-calls";

    /// <summary>
    /// A workshop.
    /// </summary>
    public static ResourceType WorkshopType { get; } = ResourceType.Parse("workshop");

    /// <summary>
    /// A kite taken in for repair.
    /// </summary>
    public static ResourceType RepairType { get; } = ResourceType.Parse("repair");

    /// <summary>
    /// A grip fitting made in a repair.
    /// </summary>
    public static ResourceType FittingType { get; } = ResourceType.Parse("fitting");

    /// <summary>
    /// Opening a workshop in an organization.
    /// </summary>
    public static Permission OpenWorkshop { get; } = Permission.Parse("workshop:open");

    /// <summary>
    /// Reading a repair.
    /// </summary>
    public static Permission ReadRepair { get; } = Permission.Parse("repair:read");

    /// <summary>
    /// Releasing a repaired kite, which is bound to a step-up gate.
    /// </summary>
    public static Permission ReleaseRepair { get; } = Permission.Parse("repair:release");

    /// <summary>
    /// Cancelling a repair.
    /// </summary>
    public static Permission CancelRepair { get; } = Permission.Parse("repair:cancel");

    /// <summary>
    /// Adding a grip fitting to a repair, which is done for a purpose resting on consent.
    /// </summary>
    public static Permission AddFitting { get; } = Permission.Parse("fitting:add");

    /// <summary>
    /// Reading a repair's grip fittings, which is done for the same purpose.
    /// </summary>
    public static Permission ReadFitting { get; } = Permission.Parse("fitting:read");

    /// <summary>
    /// The workshop's declaration.
    /// </summary>
    /// <returns>The declaration.</returns>
    public static AuthorizationDeclaration Declaration()
    {
        var declaring = new AuthorizationDeclarationBuilder();

        foreach (LawfulBasisDeclaration basis in LawfulBases.Default)
        {
            _ = declaring.LawfulBasis(basis);
        }

        foreach (string category in SensitiveCategories.Default)
        {
            _ = declaring.SensitiveCategory(category);
        }

        return declaring
            .RetentionFloor(Catalogue, TimeSpan.FromDays(365))
            .RetentionFloor(Contact, TimeSpan.FromDays(730))
            .RetentionFloor(Health, TimeSpan.FromDays(30))
            .Permission(OpenWorkshop.ToString())
            .Permission(ReadRepair.ToString())
            .Permission(ReleaseRepair.ToString())
            .Permission(CancelRepair.ToString())
            .Permission(AddFitting.ToString())
            .Permission(ReadFitting.ToString())
            .StepUpGate(ReleaseRepair.ToString(), Releasing)
            .ServesPurpose(AddFitting.ToString(), AdaptingGrips)
            .ServesPurpose(ReadFitting.ToString(), AdaptingGrips)
            .Role(
                WorkshopRoles.Manager,
                Permissions.MembershipManage.ToString(),
                Permissions.MembershipRead.ToString(),
                Permissions.GrantManage.ToString(),
                Permissions.GrantRead.ToString())
            .Role(
                WorkshopRoles.Technician,
                OpenWorkshop.ToString(),
                ReadRepair.ToString(),
                ReleaseRepair.ToString(),
                ReadFitting.ToString())
            .Role(
                WorkshopRoles.RepairOwner,
                ReadRepair.ToString(),
                CancelRepair.ToString(),
                AddFitting.ToString(),
                ReadFitting.ToString())
            .Role(WorkshopRoles.RepairCollector, ReadRepair.ToString())
            .Job(FollowUpCalls.Name, ReadRepair.ToString())
            .Job(ReadyNotices.Name, ReadRepair.ToString(), Permissions.ContactRead.ToString())
            .Relationship<RepairOwner>(
                Owner,
                RepairType.ToString(),
                WorkshopSchema.Name + ".owners",
                row => row.Owner,
                "owner",
                row => row.RepairId,
                "repair_id")
            .Relationship<RepairCollector>(
                Collector,
                RepairType.ToString(),
                WorkshopSchema.Name + ".collectors",
                row => row.Collector,
                "collector",
                row => row.RepairId,
                "repair_id")
            .Resource<Workshop>(WorkshopType.ToString(), type => type
                .BelongsToOrganization()
                .Discloses()
                .Purpose(CarryingOutRepairs, "contractual-obligation", data: [Catalogue], subjects: [Staff]))
            .Resource<Repair>(RepairType.ToString(), type => type
                .ContainedIn(WorkshopType.ToString())
                .Derivation(Owner, WorkshopRoles.RepairOwner)
                .Derivation(Collector, WorkshopRoles.RepairCollector)
                .Capabilities(ReadRepair.ToString(), ReleaseRepair.ToString(), CancelRepair.ToString())
                .Encrypted(repair => repair.ContactName, repair => repair.Customer, Contact, name: ContactName)
                .Purpose(CarryingOutRepairs, "contractual-obligation", data: [Contact], subjects: [Customers])
                .Purpose(
                    FollowingUpRepairs,
                    "legitimate-interest",
                    assessment: FollowUpAssessment,
                    data: [Contact],
                    subjects: [Customers]))
            .Resource<Fitting>(FittingType.ToString(), type => type
                .ContainedIn(RepairType.ToString())
                .Sensitive(Health)
                .Encrypted(fitting => fitting.Need, fitting => fitting.Customer, Health, name: GripNeed)
                .Purpose(
                    AdaptingGrips,
                    "consent",
                    data: [Health],
                    subjects: [Customers],
                    consent: ConsentKind.Written))
            .Build();
    }
}
