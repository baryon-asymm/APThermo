using System.Buffers.Binary;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Bounded versioned binary messages on the private duplex control channel.</summary>
internal static class Wire
{
    internal const byte Ready = 1;
    internal const byte Start = 2;
    internal const byte Abort = 3;
    internal const byte Exit = 4;
    internal const byte Release = 5;
    internal const byte Error = 6;
    internal const byte Terminate = 7;
    internal const byte Started = 8;
    private const int HeaderSize = 10;
    private const int MaxPayload = 4096;

    internal static async Task SendAsync(Stream stream, byte kind, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        if (kind is < Ready or > Started || payload.Length > MaxPayload)
        {
            throw new InvalidDataException("invalid process-owner message");
        }
        var header = new byte[HeaderSize];
        "APTO"u8.CopyTo(header);
        header[4] = 1;
        header[5] = kind;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(6), (uint)payload.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(payload, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    internal static async Task<(byte Kind, byte[] Payload)> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[HeaderSize];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6));
        if (!header.AsSpan(0, 4).SequenceEqual("APTO"u8) || header[4] != 1 ||
            header[5] is < Ready or > Started || length > MaxPayload)
        {
            throw new InvalidDataException("invalid process-owner frame header");
        }
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return (header[5], payload);
    }

    internal static byte[] Code(int value)
    {
        var data = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        return data;
    }
}
