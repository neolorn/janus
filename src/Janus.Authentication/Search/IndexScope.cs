using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// Which subjects a search may answer.
/// </summary>
/// <param name="Organization">
/// The organization whose current members and records are answered, or nothing for
/// every account and record the index holds, as a search asked in the administrative
/// organization answers.
/// </param>
/// <param name="Accounts">Whether accounts are answered.</param>
/// <param name="Records">Whether person records are answered.</param>
/// <remarks>Implements IDN-SRCH-002 (D-256).</remarks>
internal readonly record struct IndexScope(OrganizationId? Organization, bool Accounts, bool Records);
