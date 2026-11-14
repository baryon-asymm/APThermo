using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace APThermo.Execution.Chunks;

/// <summary>Versioned, length-framed byte encoding for a complete declaration inventory.</summary>
internal static class BufferIdentityFrame
{
    public static void Integer(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    public static void Text(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Integer(hash, bytes.LongLength);
        hash.AppendData(bytes);
    }

    /// <summary>Hash every original bit, in bounded spans even when the complete array exceeds a span's byte limit.</summary>
    public static void InitialBytes<T>(IncrementalHash hash, T[]? host) where T : unmanaged
    {
        Integer(hash, host is null ? 0 : host.LongLength * Unsafe.SizeOf<T>());
        if (host is null) { return; }
        var maximumElements = Math.Max(1, 65536 / Unsafe.SizeOf<T>());
        var start = 0;
        while (start < host.Length)
        {
            var length = Math.Min(maximumElements, host.Length - start);
            hash.AppendData(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(host, start, length)));
            start += length;
        }
    }
}