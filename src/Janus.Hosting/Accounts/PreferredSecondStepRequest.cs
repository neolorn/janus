using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Which second step the account asks to be offered first.
/// </summary>
/// <param name="Method">The second factor enrolled on the account, by its identifier.</param>
/// <remarks>Implements IDN-ATTR-008.</remarks>
internal sealed record PreferredSecondStepRequest(Guid? Method);
