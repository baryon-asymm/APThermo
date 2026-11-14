using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using APThermo.Execution.Chunks;
using ILGPU.Runtime;

namespace APThermo.Execution.Tests;

/// <summary>Initial byte identity, full declaration framing and pre-allocation lifecycle controls on CPU buffers.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BufferIdentityTests
{
    private static readonly double[] ChangedSeed = [5.0, 6.0, 7.0, 8.0];
    private static readonly IReadOnlyDictionary<ChunkTransfer, bool> InitialByteRoles = new Dictionary<ChunkTransfer, bool>
    {
        [ChunkTransfer.Input] = true,
        [ChunkTransfer.Constant] = true,
        [ChunkTransfer.InputOutput] = true,
        [ChunkTransfer.Output] = false,
        [ChunkTransfer.ClearedOutput] = false,
        [ChunkTransfer.Scratch] = false,
    };

    private sealed class UnsupportedBuffer : IChunkBuffer
    {
        public long BytesPerCase => 0;
        public long ElementsPerCase => 0;
        public bool AppendIdentity(IncrementalHash hash)
        {
            hash.AppendData([123]);
            return false;
        }

        public void Allocate(Accelerator accelerator, int chunk) { }
        public void UploadChunk(int offset, int length) { }
        public void DownloadChunk(int offset, int length) { }
        public void Dispose() { }
    }

    /// <summary>The actual declaration modes generate the inventory; a new mode must be handled explicitly.</summary>
    public static TheoryData<int> Directions()
    {
        var data = new TheoryData<int>();
        foreach (var direction in Enum.GetValues<ChunkTransfer>()) { data.Add((int)direction); }
        return data;
    }

    /// <summary>Only modes whose contract reads initial host bytes generate the bit-payload controls.</summary>
    public static TheoryData<int> InputDirections()
    {
        var data = new TheoryData<int>();
        foreach (var direction in Enum.GetValues<ChunkTransfer>())
        {
            if (InitialByteRoles.TryGetValue(direction, out var reads) && reads) { data.Add((int)direction); }
        }
        return data;
    }

    /// <summary>A newly declared mode must receive an independently specified data/structure role before identity reuse is accepted.</summary>
    [Fact]
    public void TheIndependentRoleTableCoversEveryDeclaredDirection() =>
        Assert.Equal(Enum.GetValues<ChunkTransfer>().Order(), InitialByteRoles.Keys.Order());

    /// <summary>All modes contribute structure, while only the three initial-input directions contribute host values.</summary>
    [Theory]
    [MemberData(nameof(Directions))]
    public void EveryDeclaredDirectionHasItsSpecifiedContentAndShapeIdentity(int mode)
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var direction = (ChunkTransfer)mode;
        double[] host = [1.0, 2.0, 3.0, 4.0];
        var original = Identity(cpu.IlgpuAccelerator, direction, host, 2);
        Assert.Equal(original, Identity(cpu.IlgpuAccelerator, direction, (double[])host.Clone(), 2));
        host[2] = 7.0;
        var changed = Identity(cpu.IlgpuAccelerator, direction, host, 2);
        Assert.Equal(ReadsInitialBytes(direction), original != changed);
        if (direction != ChunkTransfer.Scratch)
        {
            Assert.NotEqual(original, Identity(cpu.IlgpuAccelerator, direction, new double[5], 2));
        }

        if (direction != ChunkTransfer.Constant)
        {
            Assert.NotEqual(original, Identity(cpu.IlgpuAccelerator, direction, [1.0, 2.0, 3.0, 4.0], 1));
        }

        Assert.NotEqual(Identity(cpu.IlgpuAccelerator, direction, new int[4], 2),
            Identity(cpu.IlgpuAccelerator, direction, new float[4], 2));
    }

    /// <summary>Each initial-input mode retains signed zero and distinct NaN payload bits without floating-point normalization.</summary>
    [Theory]
    [MemberData(nameof(InputDirections))]
    public void InitialInputDirectionsRetainSignedZeroAndNanPayloads(int mode)
    {
        var direction = (ChunkTransfer)mode;
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var zero = Identity<double>(cpu.IlgpuAccelerator, direction, [0.0], 1);
        Assert.NotEqual(zero, Identity<double>(cpu.IlgpuAccelerator, direction, [-0.0], 1));
        var first = BitConverter.Int64BitsToDouble(0x7ff8000000000001);
        var second = BitConverter.Int64BitsToDouble(0x7ff8000000000002);
        Assert.NotEqual(Identity<double>(cpu.IlgpuAccelerator, direction, [first], 1),
            Identity<double>(cpu.IlgpuAccelerator, direction, [second], 1));
    }

    /// <summary>The wire format is checked independently with BinaryWriter and a one-shot SHA-256 over known bytes.</summary>
    [Fact]
    public void OneInputMatchesTheIndependentLengthFramedWireMessage()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        byte[] input = [0x10, 0x20];
        using var message = new MemoryStream();
        using (var writer = new BinaryWriter(message, Encoding.UTF8, leaveOpen: true))
        {
            WriteText(writer, "APThermo.ChunkBuffers.InputIdentity.v1");
            writer.Write(1L);
            writer.Write((long)ChunkTransfer.Input);
            WriteText(writer, typeof(byte).AssemblyQualifiedName!);
            writer.Write(1L);
            writer.Write(2L);
            writer.Write(2L);
            writer.Write(2L);
            writer.Write(input);
        }

        var expected = Convert.ToHexString(SHA256.HashData(message.ToArray()));
        Assert.Equal(expected, Identity(cpu.IlgpuAccelerator, ChunkTransfer.Input, input, 2));
    }

    /// <summary>Order, direction, count and boundaries distinguish inventories even when concatenated bytes are equal.</summary>
    [Fact]
    public void DeclarationFramingPreventsConcatenationAndOrderingAliases()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var first = new ChunkBuffers(cpu.IlgpuAccelerator);
        using var reversed = new ChunkBuffers(cpu.IlgpuAccelerator);
        _ = first.Input<int>([1], 1);
        _ = first.Constant<int>([2]);
        _ = reversed.Constant<int>([2]);
        _ = reversed.Input<int>([1], 1);
        Assert.NotEqual(first.InputIdentity(), reversed.InputIdentity());
        using var left = new ChunkBuffers(cpu.IlgpuAccelerator);
        using var right = new ChunkBuffers(cpu.IlgpuAccelerator);
        using var joined = new ChunkBuffers(cpu.IlgpuAccelerator);
        _ = left.Input<byte>([1, 2], 1);
        _ = left.Input<byte>([3], 1);
        _ = right.Input<byte>([1], 1);
        _ = right.Input<byte>([2, 3], 1);
        _ = joined.Input<byte>([1, 2, 3], 1);
        Assert.NotEqual(left.InputIdentity(), right.InputIdentity());
        Assert.NotEqual(left.InputIdentity(), joined.InputIdentity());
        var directionKeys = Enum.GetValues<ChunkTransfer>()
            .Select(mode => Identity(cpu.IlgpuAccelerator, mode, new byte[4], 2)).ToArray();
        Assert.Equal(directionKeys.Length, directionKeys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>First, middle and final bytes of a multi-window input must all affect the digest.</summary>
    [Fact]
    public void EveryPartOfAnInputLargerThanThreeHashWindowsContributes()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var input = new byte[3 * 65536 + 19];
        var original = Identity(cpu.IlgpuAccelerator, ChunkTransfer.Input, input, 1);
        foreach (var index in new[] { 0, input.Length / 2, input.Length - 1 })
        {
            var changed = (byte[])input.Clone();
            changed[index] = 1;
            Assert.NotEqual(original, Identity(cpu.IlgpuAccelerator, ChunkTransfer.Input, changed, 1));
        }
    }

    /// <summary>A seed's initial key survives as a value; downloaded new seed bytes get a different identity in a new run.</summary>
    [Fact]
    public void DownloadedInputOutputBytesCannotReplaceTheInitialSeedIdentity()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        double[] seed = [1.0, 2.0, 3.0, 4.0];
        var original = (double[])seed.Clone();
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        var buffer = buffers.InputOutput(seed, 1);
        var key = buffers.InputIdentity();
        Assert.Equal(Identity(cpu.IlgpuAccelerator, ChunkTransfer.InputOutput, original, 1), key);
        buffers.Allocate(seed.Length);
        buffers.UploadChunk(0, seed.Length);
        buffer.View.CopyFromCPU(ChangedSeed);
        buffers.DownloadChunk(0, seed.Length);
        Assert.Equal(ChangedSeed, seed);
        _ = Assert.Throws<InvalidOperationException>(buffers.InputIdentity);
        Assert.NotEqual(key, Identity(cpu.IlgpuAccelerator, ChunkTransfer.InputOutput, seed, 1));
        Assert.Equal(key, Identity(cpu.IlgpuAccelerator, ChunkTransfer.InputOutput, original, 1));
    }

    /// <summary>Capture is refused after Allocate/dispose; a constant is read before its immediate upload.</summary>
    [Fact]
    public void CaptureMustPrecedeAllocationAndDisposal()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var allocated = new ChunkBuffers(cpu.IlgpuAccelerator);
        var constant = allocated.Constant<int>([7, 8]);
        Assert.NotNull(allocated.InputIdentity());
        allocated.Allocate(1);
        _ = Assert.Throws<InvalidOperationException>(allocated.InputIdentity);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        _ = Assert.Throws<InvalidOperationException>(() => constant.AppendIdentity(hash));
        using var disposed = new ChunkBuffers(cpu.IlgpuAccelerator);
        var declaration = disposed.Input<int>([1], 1);
        disposed.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(disposed.InputIdentity);
        declaration.Dispose();
        _ = Assert.Throws<InvalidOperationException>(() => declaration.AppendIdentity(hash));
    }

    /// <summary>A custom declaration may append a prefix and still decline support; no digest is returned.</summary>
    [Fact]
    public void AnUnsupportedDeclarationDisablesTheWholeIdentity()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        _ = buffers.Input<int>([1], 1);
        var field = typeof(ChunkBuffers).GetField("_buffers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var declarations = Assert.IsType<List<IChunkBuffer>>(field.GetValue(buffers));
        declarations.Add(new UnsupportedBuffer());
        Assert.Null(buffers.InputIdentity());
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var missing = new ChunkBuffer<int>(null, 1, ChunkTransfer.Input);
        using var unknown = new ChunkBuffer<int>([1], 1, (ChunkTransfer)int.MaxValue);
        Assert.False(missing.AppendIdentity(hash));
        Assert.False(unknown.AppendIdentity(hash));
        var nextUnclassified = (ChunkTransfer)(InitialByteRoles.Keys.Max(mode => (int)mode) + 1);
        using var next = new ChunkBuffer<int>([1], 1, nextUnclassified);
        Assert.False(next.AppendIdentity(hash));
    }

    /// <summary>The exposed nullable value is the existing quarter-limit target and None stays unbounded.</summary>
    [Fact]
    public void TargetDurationExposesTheExistingBudget()
    {
        Assert.Null(LaunchBudget.None.TargetDuration);
        Assert.Equal(TimeSpan.FromSeconds(2), LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(8)).TargetDuration);
    }

    private static string Identity<T>(Accelerator accelerator, ChunkTransfer direction, T[] host, long stride) where T : unmanaged
    {
        using var buffers = new ChunkBuffers(accelerator);
        _ = Declare(buffers, direction, host, stride);
        return Assert.IsType<string>(buffers.InputIdentity());
    }

    private static ChunkBuffer<T> Declare<T>(ChunkBuffers buffers, ChunkTransfer direction, T[] host, long stride) where T : unmanaged => direction switch
    {
        ChunkTransfer.Input => buffers.Input(host, stride),
        ChunkTransfer.Output => buffers.Output(host, stride),
        ChunkTransfer.ClearedOutput => buffers.ClearedOutput(host, stride),
        ChunkTransfer.InputOutput => buffers.InputOutput(host, stride),
        ChunkTransfer.Scratch => buffers.Scratch<T>(stride),
        ChunkTransfer.Constant => buffers.Constant(host),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private static bool ReadsInitialBytes(ChunkTransfer direction) => InitialByteRoles[direction];

    private static void WriteText(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.LongLength);
        writer.Write(bytes);
    }
}