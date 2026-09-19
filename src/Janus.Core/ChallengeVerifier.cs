using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a deployment registers to judge a bot-defence challenge: a callback that
/// takes the token the challenge produced and answers whether it passed.
/// </summary>
/// <param name="Passes">Whether this token passes.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008 and LIB-HOST-001. The library ships no challenge: where
/// no verifier is registered the signal is audited and nothing is shown, because a
/// puzzle the library cannot judge would be friction without benefit.
/// </remarks>
public sealed record ChallengeVerifier(Func<string, CancellationToken, ValueTask<bool>> Passes);
