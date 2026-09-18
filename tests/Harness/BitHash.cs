using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace APThermo.Harness;

/// <summary>
/// SHA-256 over the little-endian bytes of the values added, in the order they were added; a string enters as its
/// UTF-8 bytes, with no length prefix or separator (BOOT.md, "bits are raw bits"). Reproduces, value for value, how
/// every bit snapshot of the tree was hashed before this node existed: a double as the little-endian bytes of
/// <see cref="BitConverter.DoubleToInt64Bits(double)"/>, an int as its little-endian bytes. One instance computes one
/// digest; call <see cref="ToHex"/> once. <see cref="Fields"/> stays readable afterward, for a caller that wants the
/// per-case field dump of the approval level below (BOOT.md, "a field dump is a caller's opt-in").
/// </summary>
public sealed class BitHash
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly List<string> _fields = [];

    public BitHash Add(double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        _hash.AppendData(bytes);
        _fields.Add(value.ToString("R", CultureInfo.InvariantCulture));
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
        _fields.Add(value.ToString(CultureInfo.InvariantCulture));
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

    /// <summary>One byte, 1 for true and 0 for false, as <see cref="BinaryWriter.Write(bool)"/> writes it (the front door tests node's presence flags).</summary>
    public BitHash Add(bool value)
    {
        Span<byte> bytes = [value ? (byte)1 : (byte)0];
        _hash.AppendData(bytes);
        _fields.Add(value ? "true" : "false");
        return this;
    }

    /// <summary>Its UTF-8 bytes, as the recorded snapshots hash a string.</summary>
    public BitHash Add(string text)
    {
        _hash.AppendData(Encoding.UTF8.GetBytes(text));
        _fields.Add(text);
        return this;
    }

    /// <summary>The SHA-256 of everything added so far, lower-case hexadecimal. Releases the underlying algorithm; <see cref="Fields"/> stays readable.</summary>
    public string ToHex()
    {
        var digest = _hash.GetHashAndReset();
        _hash.Dispose();
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// Every value added so far, as text in the order added: a double round-trip ("R", invariant culture) so a diagnostic
    /// dump can be pasted back into an experiment, an int in invariant culture, a bool as "true"/"false", a string as
    /// added. Readable before or after <see cref="ToHex"/>; this list is not what is hashed and folds no formula of its
    /// own (BOOT.md, "bits are raw bits" — nothing here rounds or tolerates, it only renders what was already added).
    /// </summary>
    public IReadOnlyList<string> Fields => _fields;
}
