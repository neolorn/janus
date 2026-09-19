namespace Janus.Authentication.Sending;

/// <summary>
/// What answers whether a source address falls in a published datacenter range. The
/// answer comes from a file read in process, never from a call to a third party.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-008 and INT-GEN-006.</remarks>
internal interface IDatacenterRanges
{
    /// <summary>
    /// Whether one address falls in a range the file lists.
    /// </summary>
    /// <param name="source">The address.</param>
    /// <returns>Whether it does; false where no file is available.</returns>
    bool Contains(string source);
}
