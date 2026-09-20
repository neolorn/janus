namespace Janus.Hosting.Accounts;

/// <summary>
/// Which second step the account asked to be offered first.
/// </summary>
/// <param name="Preferred">
/// The credential offered first, or nothing where the account has no second step.
/// </param>
/// <remarks>Implements IDN-ATTR-008 and REG-ACCT-001.</remarks>
internal sealed record SecondStepView(string? Preferred);
