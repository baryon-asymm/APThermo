namespace APThermo.Harness;

/// <summary>Continuously captures raw process bytes and permits a synchronized partial snapshot on interruption.</summary>
internal sealed class ProcessOutput : IDisposable
{
    private readonly Stream _stream;
    private readonly List<byte> _buffer = [];
    private readonly Lock _gate = new();
    private int _stopped;

    public Task Completion { get; }

    public ProcessOutput(Stream stream)
    {
        _stream = stream;
        Completion = ReadAsync();
    }

    public byte[] Snapshot()
    {
        lock (_gate)
        {
            return [.. _buffer];
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            _stream.Dispose();
        }
    }

    public void Dispose() => Stop();

    private async Task ReadAsync()
    {
        var chunk = new byte[8192];
        try
        {
            int length;
            while ((length = await _stream.ReadAsync(chunk).ConfigureAwait(false)) != 0)
            {
                lock (_gate)
                {
                    _buffer.AddRange(chunk.AsSpan(0, length));
                }
            }
        }
        catch (Exception problem) when (Volatile.Read(ref _stopped) != 0
            && problem is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // A bounded cleanup closes a reader even if a descendant still owns the pipe's writer.
        }
    }
}
