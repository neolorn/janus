using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Janus.Hosting.Bff;

/// <summary>
/// The response body while stage 11 may still have to replace it: what the endpoint
/// writes goes through until the gate conceals a refusal, and nothing goes through
/// after.
/// </summary>
/// <param name="carried">The body the response is written to.</param>
/// <param name="refusals">What says whether a refusal was concealed.</param>
/// <remarks>
/// Implements BFF-ERR-003 and OPS-ENV-002. Nothing is held back while nothing is
/// concealed, so a stream the endpoint flushes reaches the browser as it is written.
/// </remarks>
internal sealed class WithheldBody(IHttpResponseBodyFeature carried, ConcealedRefusals refusals)
    : Stream, IHttpResponseBodyFeature
{
    private PipeWriter? _writer;

    private bool _finished;

    /// <summary>
    /// Whether anything the endpoint wrote went through to the response.
    /// </summary>
    public bool Passed { get; private set; }

    /// <inheritdoc/>
    public Stream Stream => this;

    /// <inheritdoc/>
    public PipeWriter Writer => _writer ??= PipeWriter.Create(this, new StreamPipeWriterOptions(leaveOpen: true));

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private bool Withholding => refusals.Correlation is not null;

    /// <inheritdoc/>
    public void DisableBuffering() => carried.DisableBuffering();

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Withholding ? Task.CompletedTask : carried.StartAsync(cancellationToken);

    /// <inheritdoc/>
    public Task SendFileAsync(
        string path,
        long offset,
        long? count,
        CancellationToken cancellationToken = default)
    {
        if (Withholding)
        {
            return Task.CompletedTask;
        }

        Passed = true;

        return carried.SendFileAsync(path, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CompleteAsync()
    {
        await FinishAsync(CancellationToken.None).ConfigureAwait(false);

        if (!Withholding)
        {
            await carried.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes through what the endpoint left in the writer, as the response would have
    /// at its end.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task FinishAsync(CancellationToken cancellationToken)
    {
        if (_writer is null || _finished)
        {
            return;
        }

        _finished = true;
        _ = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _writer.CompleteAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        if (!Withholding)
        {
            carried.Stream.Flush();
        }
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Withholding ? Task.CompletedTask : carried.Stream.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (Withholding)
        {
            return;
        }

        Passed |= !buffer.IsEmpty;
        carried.Stream.Write(buffer);
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (Withholding)
        {
            return ValueTask.CompletedTask;
        }

        Passed |= !buffer.IsEmpty;

        return carried.Stream.WriteAsync(buffer, cancellationToken);
    }
}
