namespace Janus.Privacy.Erasures;

/// <summary>
/// What a replay of the off-host erasure ledger found, line by line.
/// </summary>
/// <param name="Reapplied">The erasures the restore had taken away and the replay carried out again.</param>
/// <param name="Standing">The lines whose erasure the restored database still holds, a repeated line included.</param>
/// <param name="Absent">The lines naming an account the restored database does not hold.</param>
/// <remarks>Implements DR-016 AC3.</remarks>
internal sealed record ReplayedErasures(int Reapplied, int Standing, int Absent);
