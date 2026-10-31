namespace Janus.Hosting.Bff;

/// <summary>
/// What a profile leaves on a request while the framework's authentication runs inside
/// it, the one place the provider acts on a request.
/// </summary>
/// <remarks>
/// Implements BFF-ORDER-001 (D-230). The framework adds an authentication middleware of
/// its own ahead of the host's middleware wherever the host calls none, and the provider
/// would answer its routes there, before any stage. The mark is set after the profile's
/// stages and taken off again once the authentication inside the profile has run, so the
/// provider passes on every request that does not carry it. The type is the library's
/// own, so nothing a host writes can put it on a request.
/// </remarks>
internal sealed class ProfilePassed
{
    /// <summary>
    /// The one mark, which carries nothing.
    /// </summary>
    public static readonly ProfilePassed Mark = new();

    private ProfilePassed()
    {
    }
}
