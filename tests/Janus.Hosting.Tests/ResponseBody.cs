using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Tests;

/// <summary>
/// Where a response is written. It can be read while it is still being written,
/// which is what a stream a test is watching needs.
/// </summary>
internal sealed class ResponseBody : Stream
{
    private readonly Lock _lock = new();
    private readonly MemoryStream _written = new();

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            lock (_lock)
            {
                return _written.Length;
            }
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get => Length;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Everything written so far.
    /// </summary>
    /// <returns>The text.</returns>
    public string Taken()
    {
        lock (_lock)
        {
            return Encoding.UTF8.GetString(_written.ToArray());
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            _written.Write(buffer, offset, count);
        }
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (_lock)
        {
            _written.Write(buffer);
        }
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _written.Dispose();
        }

        base.Dispose(disposing);
    }
}
