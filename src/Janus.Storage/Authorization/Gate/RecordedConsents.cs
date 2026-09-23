using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Storage.Privacy.Consents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// What a subject consented to for one purpose, over the <c>consents</c> table.
/// </summary>
/// <param name="context">The context the row is read on.</param>
/// <remarks>
/// Implements PRIV-SENS-002 and CONV-DESIGN-003. It reads the one record the gate
/// evaluates, by the key the table is held under, and nothing else.
/// </remarks>
internal sealed class RecordedConsents(StoreContext context) : IRecordedConsents
{
    /// <inheritdoc/>
    public async ValueTask<ConsentRecord?> OfAsync(
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken) =>
        await context.Consents
            .AsNoTracking()
            .Where(consent => consent.Subject == subject && consent.Purpose == purpose)
            .Select(consent => new ConsentRecord(
                consent.Purpose,
                consent.NoticeVersion,
                consent.Mechanism,
                consent.Kind,
                consent.GrantedAt,
                consent.WithdrawnAt,
                consent.SupersededAt))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
