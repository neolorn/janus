using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where the browsers an account knows are read and written. The row holds the
/// fingerprint of the token and never the token, so a dump of the table yields no
/// usable trust.
/// </summary>
/// <remarks>Implements AUTH-FACT-015, AUTH-FACT-016 and CONV-DESIGN-003.</remarks>
internal interface IDeviceStore
{
    /// <summary>
    /// Reads the browser a token stands for.
    /// </summary>
    /// <param name="fingerprint">The fingerprint of the token presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The browser, or nothing where no row carries that fingerprint.</returns>
    ValueTask<Device?> FindByFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one browser by its identifier.
    /// </summary>
    /// <param name="id">Which browser.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The browser, or nothing where there is none.</returns>
    ValueTask<Device?> FindAsync(DeviceId id, CancellationToken cancellationToken);

    /// <summary>
    /// Every browser of an account that has not lapsed or been revoked.
    /// </summary>
    /// <param name="subject">Whose browsers.</param>
    /// <param name="now">The instant a lapse is measured against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The browsers.</returns>
    ValueTask<IReadOnlyList<Device>> StandingOfAsync(
        SubjectId subject,
        System.DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a browser the account now knows, against the fingerprint of the token
    /// its browser carries.
    /// </summary>
    /// <param name="device">The browser.</param>
    /// <param name="fingerprint">The fingerprint of its token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(Device device, byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a browser made onto its row.
    /// </summary>
    /// <param name="device">The browser as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(Device device, CancellationToken cancellationToken);
}
