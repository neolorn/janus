using System;

namespace Janus.Core;

/// <summary>
/// The one place the secrets read at startup are held between their uses. A use
/// borrows a secret for its own length and keeps nothing.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007 and CONV-LAYOUT-002. Every secret the ring
/// holds is one the host's own <see cref="ISecretSource"/> supplied, so resolving it
/// exposes nothing the host does not already hold. A read before the ring is filled, or
/// after it is cleared when the application stops or a command ends, is a fault.
/// </remarks>
public interface IKeyRing
{
    /// <summary>
    /// Lends a declared social provider's credential for the length of one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="provider">The provider's name, as the secret source was asked for it.</param>
    /// <param name="use">
    /// What is done with the credential, which keeps nothing of it once it returns.
    /// </param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>socialProvider.&lt;provider&gt;</c> where the ring holds no credential for it.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowProviderCredential<TValue>(string provider, Func<ProviderCredential, TValue> use);
}
