using System;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The grace window an account is in: why it was entered and when.
/// </summary>
/// <param name="By">Why the window was entered, which decides who may end it.</param>
/// <param name="Since">When it began.</param>
/// <remarks>Implements IDN-LIFE-003, IDN-LIFE-014 and chapter 10 section 5.12b.</remarks>
internal sealed record HeldDeletion(DeletionOrigin By, DateTimeOffset Since);
