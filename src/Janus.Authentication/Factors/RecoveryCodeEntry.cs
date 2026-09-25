using System;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// One code of a set as it is stored: the hash, and when it was spent.
/// </summary>
/// <param name="Hash">The hash, computed as a password's is.</param>
/// <param name="UsedAt">When it was spent, and nothing where it has not been.</param>
/// <remarks>Implements AUTH-FACT-008.</remarks>
[NeverLogged]
internal sealed record RecoveryCodeEntry(PasswordHash Hash, DateTimeOffset? UsedAt);
