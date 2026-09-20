using System;

namespace Janus.Core;

/// <summary>
/// What a loss report leaves behind: the credential is refused from this instant, and
/// gone when the notified window ends.
/// </summary>
/// <param name="Credential">Which credential was reported.</param>
/// <param name="InvalidatesAt">When the window ends.</param>
/// <remarks>Implements AUTH-RECOV-007 and D-141.</remarks>
public sealed record LossReported(AuthenticatorId Credential, DateTimeOffset InvalidatesAt);
