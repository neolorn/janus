using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account an identifier found.
/// </summary>
/// <param name="Subject">Its subject.</param>
/// <remarks>Implements IDN-READ-003 and chapter 09 section 8a.</remarks>
internal sealed record FoundAccountView(Guid Subject);
