using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's courier, which carries the post room's letters by mail and by text and
/// tells the library what became of each (INT-SMS-005): a letter it could not deliver
/// gives back what asking for it counted against the sending restrictions. Once the
/// library has its report, the letter and the places it carried leave the post room, so
/// nothing a message carried, a one-time code or a sign-in link among it, stays at rest
/// past its outcome.
/// </summary>
public sealed class Courier
{
    private readonly IServiceScopeFactory _scopes;

    internal Courier(IServiceScopeFactory scopes) => _scopes = scopes;

    /// <summary>
    /// Reports what became of a letter.
    /// </summary>
    /// <param name="reference">The reference the letter was taken under.</param>
    /// <param name="delivered">Whether it was delivered, or failed for good.</param>
    /// <param name="cancellationToken">Abandons the report.</param>
    /// <returns>
    /// Whether the library held a message under the reference, or the failure that
    /// stopped the report, which leaves the letter to be reported again.
    /// </returns>
    public async Task<Result<bool>> ReportAsync(string? reference, bool delivered, CancellationToken cancellationToken)
    {
        if (!SendReference.TryParse(reference, out SendReference sent))
        {
            return Result.Success(false);
        }

        AsyncServiceScope scope = _scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result<bool> reported = await scope.ServiceProvider
                .GetRequiredService<ISendOutcomes>()
                .ReportAsync(sent, delivered, cancellationToken)
                .ConfigureAwait(false);

            if (reported.Match(_ => true, _ => false))
            {
                await DeliveredAsync(scope.ServiceProvider.GetRequiredService<WorkshopContext>(), sent.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            return reported;
        }
    }

    // The letter leaves the post room with its places, both or neither.
    private static async Task DeliveredAsync(WorkshopContext workshop, string reference, CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await workshop.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            _ = await workshop.Places
                .Where(place => place.Reference == reference)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            _ = await workshop.Post
                .Where(letter => letter.Reference == reference)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
