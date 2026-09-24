using System.Threading;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The copy of the IP-to-city database this process resolves addresses against.
/// </summary>
/// <remarks>
/// Implements INT-GEN-006. The copy is read in process and held for the life of the
/// process, one for every scope, and is replaced whole when the file is read again, so
/// a request resolves against one file and never against two.
/// </remarks>
internal sealed class LocationCopy
{
    private LocationFile? _held;
    private int _read;

    /// <summary>
    /// The file held, or nothing where none has been read.
    /// </summary>
    public LocationFile? Held => Volatile.Read(ref _held);

    /// <summary>
    /// Whether this process has read the file, or tried to, at least once.
    /// </summary>
    public bool Read => Volatile.Read(ref _read) is not 0;

    /// <summary>
    /// Records that the file was read, or tried.
    /// </summary>
    public void Tried() => Volatile.Write(ref _read, 1);

    /// <summary>
    /// Holds a file in place of the one held before.
    /// </summary>
    /// <param name="file">The file.</param>
    public void Hold(LocationFile file) => Volatile.Write(ref _held, file);
}
