namespace Janus.Core.Configuration;

/// <summary>
/// An operation that works on any family of the catalogue, handed each one typed.
/// </summary>
/// <typeparam name="TResult">What the operation answers.</typeparam>
/// <remarks>
/// Implements CONV-CODE-004, as <see cref="ISettingOperation{TResult}"/> does for the
/// keys that exist once: a generic method reaches each family with its own type where
/// reflection would otherwise be needed.
/// </remarks>
internal interface ISettingFamilyOperation<out TResult>
{
    /// <summary>
    /// Works on one family.
    /// </summary>
    /// <typeparam name="TValue">The type of a member's value.</typeparam>
    /// <param name="family">The family.</param>
    /// <returns>What the operation answers.</returns>
    TResult On<TValue>(SettingFamily<TValue> family);
}
