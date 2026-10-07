namespace Janus.Authentication.Sending;

/// <summary>
/// One admitted message of a send as the ledger counts it: the hash of the reference it
/// is counted under, and what it counts against and spends.
/// </summary>
/// <param name="Reference">The hash of the message's correlation reference.</param>
/// <param name="Plan">What the message counts against and spends.</param>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed record CountedMessage(byte[] Reference, SendPlan Plan);
