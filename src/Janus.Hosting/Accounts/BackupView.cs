using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Which of an account's addresses a security notice reaches, by kind.
/// </summary>
/// <param name="Emails">The setting for email.</param>
/// <param name="Phones">The setting for phone.</param>
/// <remarks>Implements REG-IDENT-002 and REG-ACCT-001.</remarks>
internal sealed record BackupView(string Emails, string Phones)
{
    /// <summary>
    /// Reads the settings.
    /// </summary>
    /// <param name="settings">What each kind is set to.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The settings are absent.</exception>
    public static BackupView Of(IReadOnlyList<BackupSelection> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new BackupView(Set(settings, IdentifierKind.Email), Set(settings, IdentifierKind.Phone));
    }

    // A named identifier is reported as the identifier, which is what the setting is.
    private static string Set(IReadOnlyList<BackupSelection> settings, IdentifierKind kind)
    {
        foreach (BackupSelection setting in settings)
        {
            if (setting.Kind != kind)
            {
                continue;
            }

            return setting.Named is IdentifierId named
                ? named.ToString()
                : Written(setting.Choice);
        }

        return Written(BackupChoice.AllVerified);
    }

    private static string Written(BackupChoice choice) =>
        choice is BackupChoice.PrimaryOnly ? "primary-only" : "all-verified";
}
