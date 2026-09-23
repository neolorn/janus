namespace Janus.Hosting.Bff;

/// <summary>
/// Which of the deployment's applications the pipeline is mounted in. It decides the
/// one cookie attribute that differs between them and nothing else.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005. The management application has no external entry point,
/// so its cookies are withheld from every cross-site request; an application people
/// reach from a link in a message cannot be, because the link arrives as a top-level
/// navigation that has to carry the session.
/// </remarks>
public enum ApplicationKind
{
    /// <summary>
    /// The management application. Its cookies carry <c>SameSite=Strict</c>.
    /// </summary>
    Management = 0,

    /// <summary>
    /// An application reached from outside, such as by a sign-in or verification
    /// link. Its cookies carry <c>SameSite=Lax</c>.
    /// </summary>
    Public = 1,
}
