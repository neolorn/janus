using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// The location file a deployment supplies, held as the text a test wrote, or a
/// refusal where a test stands in for a file that could not be opened.
/// </summary>
internal sealed class LocationSourceInMemory : ILocationSource
{
    /// <summary>
    /// What the file holds now.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// What opening the file answers with instead of the file.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <summary>
    /// How many times the file was opened.
    /// </summary>
    public int Opened { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<Stream>> OpenAsync(CancellationToken cancellationToken)
    {
        Opened++;

        return ValueTask.FromResult(Refusal is Error refused
            ? Result.Failure<Stream>(refused)
            : Result.Success<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Text))));
    }
}
