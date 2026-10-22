namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Where one of the host's documents stands, a type of the host's own database that its
/// context reads only through the type mapping its provider's data source carries.
/// </summary>
internal enum HostStage
{
    /// <summary>The document is still being written.</summary>
    Draft = 0,

    /// <summary>The document is out.</summary>
    Published = 1,
}
