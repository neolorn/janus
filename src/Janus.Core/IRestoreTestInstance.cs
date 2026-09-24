using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What builds the separate, throwaway database instance the automated restore test
/// restores into, and tears it down again. The library restores nothing itself: a
/// deployment registers one of these over its backups and its committed infrastructure
/// definition, and until it does every run of the test fails and is raised, since
/// nothing was restored.
/// </summary>
/// <remarks>
/// Implements DR-007, DR-008, DR-017 AC2 and LIB-EXT-001. Every
/// <c>backup.restoretest.interval</c> the library asks for an instance, opens the
/// restored database with the key-encryption and fingerprint keys it runs on, decrypts
/// the canary subject's field and finds its account by its verified email's
/// fingerprint, compares the time taken with <c>backup.restoretest.objective</c>, and
/// then asks for the instance to be torn down, whatever became of the test. The backup's
/// private key is the implementation's to fetch from the secrets manager when it
/// restores and to hold in memory only (DR-010 AC2); nothing of it reaches the library.
/// </remarks>
public interface IRestoreTestInstance
{
    /// <summary>
    /// Builds a new database instance from the committed infrastructure definition,
    /// never over the running database, and restores into it the latest base backup and
    /// the logs that follow it.
    /// </summary>
    /// <param name="cancellationToken">
    /// Abandons the restore; it is cancelled once the objective has passed, since the
    /// test has failed by then.
    /// </param>
    /// <returns>
    /// How to reach the restored database, or the failure where nothing could be
    /// restored.
    /// </returns>
    ValueTask<Result<string>> RestoreAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tears down the instance the last restore built, however far it got, so that no
    /// instance outlives its test. It is asked after every restore, one that failed or
    /// was abandoned included, once the library has closed every connection it opened
    /// to the instance.
    /// </summary>
    /// <param name="cancellationToken">Abandons the teardown.</param>
    /// <returns>
    /// Nothing once the instance is gone, or the failure where it may still stand.
    /// </returns>
    ValueTask<Result> TearDownAsync(CancellationToken cancellationToken);
}
