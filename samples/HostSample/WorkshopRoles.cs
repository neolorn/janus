namespace HostSample;

/// <summary>
/// The roles the workshop declares: the one that manages an organization's staff, the
/// one an organization grants its technicians, and the two that follow from the facts of
/// a repair.
/// </summary>
public static class WorkshopRoles
{
    /// <summary>
    /// A manager of the organization that runs a workshop, who invites its staff, reads
    /// who they are, grants them their roles and reads who can reach its records.
    /// </summary>
    public const string Manager = "workshop-manager";

    /// <summary>
    /// A technician of the organization that runs a workshop, who opens workshops, reads
    /// their repairs and releases repaired kites.
    /// </summary>
    public const string Technician = "technician";

    /// <summary>
    /// The customer whose kite a repair is, who reads and cancels it and has grip
    /// fittings made in it.
    /// </summary>
    public const string RepairOwner = "repair-owner";

    /// <summary>
    /// The person a repair's owner named to collect the kite, who reads the repair.
    /// </summary>
    public const string RepairCollector = "repair-collector";
}
