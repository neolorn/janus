namespace Janus.Storage;

/// <summary>
/// One statement of a migration the database has not applied, as the migration's
/// script runs it.
/// </summary>
/// <param name="Migration">The migration's identifier, as its history row names it.</param>
/// <param name="Statement">The statement itself, without the block that guards it in the script.</param>
/// <remarks>
/// Implements OPS-MIG-001 and CONV-DESIGN-003, as D-206 settles them.
/// </remarks>
internal sealed record PendingStatement(string Migration, string Statement);
