using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AerospacePropellantThermodynamics.Harness;

/// <summary>
/// SHA-256 over the little-endian bytes of the values added, in the order they were added; a string enters as its
/// UTF-8 bytes, with no length prefix or separator (BOOT.md, "bits are raw bits"). Reproduces, value for value, how
/// every bit snapshot of the tree was hashed before this node existed: a double as the little-endian bytes of
/// <see cref="BitConverter.DoubleToInt64Bits(double)"/>, an int as its little-endian bytes. One instance computes one
/// digest; call <see cref="ToHex"/> once and discard it.
/// </summary>
public sealed class BitHash
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public BitHash Add(double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        _hash.AppendData(bytes);
        return this;
    }

    public BitHash Add(ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    public BitHash Add(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        _hash.AppendData(bytes);
        return this;
    }

    public BitHash Add(ReadOnlySpan<int> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Its UTF-8 bytes, as the recorded snapshots hash a string.</summary>
    public BitHash Add(string text)
    {
        _hash.AppendData(Encoding.UTF8.GetBytes(text));
        return this;
    }

    /// <summary>The SHA-256 of everything added so far, lower-case hexadecimal. Releases the underlying algorithm.</summary>
    public string ToHex()
    {
        var digest = _hash.GetHashAndReset();
        _hash.Dispose();
        return Convert.ToHexStringLower(digest);
    }
}
