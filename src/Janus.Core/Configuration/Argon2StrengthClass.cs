namespace Janus.Core.Configuration;

/// <summary>
/// One (memory, iterations) pair the Argon2id floor admits. The floor is a rule over
/// the two keys together rather than a bound on either, because the pairs are of
/// equal strength.
/// </summary>
/// <param name="Memory">Memory in kibibytes.</param>
/// <param name="Iterations">Iterations.</param>
/// <remarks>Implements chapter 10 section 4.2, AUTH-PASS-007, OPS-CFG-003.</remarks>
public sealed record Argon2StrengthClass(int Memory, int Iterations);
