namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// Who signs in at a social provider, as the identity token the provider signs names
/// them, and what about the token a test makes wrong.
/// </summary>
/// <param name="Subject">The provider's own identifier for the person.</param>
/// <param name="Email">The address the provider names, where it names one.</param>
/// <param name="Verified">
/// What the provider says of the address: a boolean at Google, a string at Apple.
/// </param>
/// <param name="HostedDomain">The Google Workspace domain, where there is one.</param>
internal sealed record ProviderPerson(
    string Subject,
    string? Email = null,
    object? Verified = null,
    string? HostedDomain = null)
{
    /// <summary>
    /// The nonce the token carries in place of the one the browser was sent with.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// The client the token is addressed to in place of the deployment's.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Whether the token's lifetime has already ended.
    /// </summary>
    public bool Expired { get; init; }

    /// <summary>
    /// Whether the token names the provider's key and was signed with another.
    /// </summary>
    public bool Forged { get; init; }
}
