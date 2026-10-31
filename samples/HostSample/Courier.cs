using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's courier, which carries the post room's letters by mail and by text and
/// tells the library what became of each (INT-SMS-005): a letter it could not deliver
/// gives back what asking for it counted against the sending restrictions.
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
    /// stopped the report.
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
            return await scope.ServiceProvider
                .GetRequiredService<ISendOutcomes>()
                .ReportAsync(sent, delivered, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
