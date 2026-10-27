namespace HostSample;

/// <summary>
/// A workshop as the catalogue shows it.
/// </summary>
/// <param name="Id">The workshop's identifier.</param>
/// <param name="Name">Its name.</param>
/// <param name="Capacity">How many kites it holds for repair at once.</param>
public sealed record CatalogueEntry(string Id, string Name, int Capacity);
