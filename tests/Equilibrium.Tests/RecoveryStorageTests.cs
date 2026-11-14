using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using APThermo.Equilibrium.Recovery;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace APThermo.Equilibrium.Tests;

/// <summary>Generated layout and isolated typed-view controls for the storage-only recovery bank.</summary>
/// <param name="output">Generated layout evidence, not expected scientific values.</param>
[Collection(CpuFixture.CollectionName)]
public sealed class RecoveryStorageTests(ITestOutputHelper output)
{
    private sealed record Region(string Name, long Length, int? Offset);
    private sealed record Layout(int Species, int Elements, int Doubles, int Ints, IReadOnlyList<Region> Regions);

    /// <summary>Actual backing fields generate the byte size/alignment evidence and detect layout drift.</summary>
    [Fact]
    public void MixtureStateHasTheGeneratedDoubleBankLayout()
    {
        var fields = typeof(MixtureState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.NotEmpty(fields);
        Assert.Equal(typeof(MixtureState).GetProperties().Length, fields.Length);
        Assert.All(fields, field => Assert.Equal(typeof(double), field.FieldType));
        var size = Interop.SizeOf<MixtureState>();
        Assert.Equal(Unsafe.SizeOf<MixtureState>(), size);
        Assert.Equal(Marshal.SizeOf<MixtureState>(), size);
        Assert.Equal(fields.Length * sizeof(double), size);
        for (var index = 0; index < fields.Length; index++)
        {
            Assert.Equal(index * sizeof(double), Marshal.OffsetOf<MixtureState>(fields[index].Name).ToInt64());
        }

        output.WriteLine("R10_SIZES " + JsonSerializer.Serialize(new
        {
            mixtureState = size,
            doubles = fields.Length,
            recheckState = Unsafe.SizeOf<RecheckState>(),
            attemptPlan = Unsafe.SizeOf<AttemptPlan>(),
        }));
    }

    /// <summary>Dedicated typed subviews preserve both cases, guards, signed zero and all double/int scratch inside a kernel.</summary>
    [Fact]
    public void IsolatedTypedStatesRoundTripEveryBitInsideACpuKernel()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var guard = State(-1234.5);
        MixtureState[] expected = [State(1.0), State(101.0)];
        using var banks = accelerator.Allocate1D<MixtureState>([guard, guard, guard, guard]);
        using var input = accelerator.Allocate1D(expected);
        using var actual = accelerator.Allocate1D<MixtureState>(expected.Length);
        using var doubles = accelerator.Allocate1D(Enumerable.Repeat(-1234.5, 2 * ScratchLayout.DoublesPerCase(1, 1)).ToArray());
        using var ints = accelerator.Allocate1D(Enumerable.Repeat(-1234, 2 * ScratchLayout.IntsPerCase(1, 1)).ToArray());
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<int>,
            ArrayView<MixtureState>, ArrayView<MixtureState>, ArrayView<MixtureState>>(StoreKernel);
        kernel(expected.Length, doubles.View, ints.View, banks.View, input.View, actual.View);
        accelerator.Synchronize();
        var stored = banks.GetAsArray1D();
        var restored = actual.GetAsArray1D();
        Assert.Empty(Bits.Differences(guard, stored[0], "leading typed guard"));
        Assert.Empty(Bits.Differences(guard, stored[^1], "trailing typed guard"));
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Empty(Bits.Differences(expected[index], stored[index + 1], $"bank {index}"));
            Assert.Empty(Bits.Differences(expected[index], restored[index], $"restore {index}"));
        }

        Assert.All(doubles.GetAsArray1D(), value => Assert.True(Bits.Same(-1234.5, value)));
        Assert.All(ints.GetAsArray1D(), value => Assert.Equal(-1234, value));
    }

    /// <summary>The complete candidate state must leave the loop-live local recheck.</summary>
    [Fact]
    public void TheLocalRecheckCarriesNoMixtureStatePayload() =>
        Assert.DoesNotContain(typeof(RecheckState).GetFields(), field => field.FieldType == typeof(MixtureState));

    /// <summary>C0 was captured before source edits; every earlier region remains at its exact original offset and length.</summary>
    [Fact]
    public void TheFrozenC0DoubleAndIntLayoutIsUnchanged()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath()));
        var root = document.RootElement;
        Assert.Equal(Interop.SizeOf<MixtureState>(), root.GetProperty("mixtureStateBytes").GetInt32());
        foreach (var item in root.GetProperty("layouts").EnumerateArray())
        {
            var expected = JsonSerializer.Deserialize<Layout>(item.GetRawText())!;
            var actual = Capture(expected.Species, expected.Elements);
            Assert.Equal(expected.Doubles, actual.Doubles);
            Assert.Equal(expected.Ints, actual.Ints);
            Assert.Equal(expected.Regions, actual.Regions);
            Assert.Equal(sizeof(double) * expected.Doubles + sizeof(int) * expected.Ints + Interop.SizeOf<MixtureState>(),
                ScratchLayout.BytesPerCase(expected.Species, expected.Elements));
        }
    }

    private static Layout Capture(int species, int elements)
    {
        var count = ScratchLayout.DoublesPerCase(species, elements);
        var intCount = ScratchLayout.IntsPerCase(species, elements);
        using var doubles = CpuFixture.Shared.Accelerator.Allocate1D<double>(count);
        using var ints = CpuFixture.Shared.Accelerator.Allocate1D<int>(intCount);
        using var states = CpuFixture.Shared.Accelerator.Allocate1D<MixtureState>(1);
        doubles.MemSetToZero();
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, species, elements, states.View);
        var views = DoubleViews(scratch, "").ToArray();
        for (var index = 0; index < views.Length; index++)
        {
            if (views[index].Value.Length > 0) { views[index].Value[0] = 1000.0 + index; }
        }

        var data = doubles.GetAsArray1D();
        var regions = views.Select((view, index) => new Region(view.Key, view.Value.Length,
            view.Value.Length > 0 ? Array.IndexOf(data, 1000.0 + index) : null)).ToArray();
        return new Layout(species, elements, count, intCount, regions);
    }

    private static IEnumerable<KeyValuePair<string, ArrayView<double>>> DoubleViews(object value, string prefix)
    {
        foreach (var field in value.GetType().GetFields().OrderBy(field => field.MetadataToken))
        {
            var name = prefix + field.Name;
            var child = field.GetValue(value)!;
            if (field.FieldType == typeof(ArrayView<double>))
            {
                yield return new KeyValuePair<string, ArrayView<double>>(name, (ArrayView<double>)child);
            }
            else if (field.FieldType.Namespace == typeof(EquilibriumScratch).Namespace)
            {
                foreach (var view in DoubleViews(child, name + ".")) { yield return view; }
            }
        }
    }

    private static void StoreKernel(Index1D index, ArrayView<double> doubles, ArrayView<int> ints,
                                    ArrayView<MixtureState> banks, ArrayView<MixtureState> expected, ArrayView<MixtureState> actual)
    {
        var doubleCount = ScratchLayout.DoublesPerCase(1, 1);
        var intCount = ScratchLayout.IntsPerCase(1, 1);
        var scratch = EquilibriumScratch.Slice(doubles.SubView(index * doubleCount, doubleCount),
            ints.SubView(index * intCount, intCount), 1, 1, banks.SubView(index + 1, 1));
        scratch.Recovery.BestState[0] = expected[index];
        actual[index] = scratch.Recovery.BestState[0];
    }

    private static string FixturePath([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "RecoveryLayoutC0.json");

    private static MixtureState State(double marker)
    {
        object state = default(MixtureState);
        foreach (var property in typeof(MixtureState).GetProperties()) { property.SetValue(state, marker++); }
        var result = (MixtureState)state;
        result.InternalEnergy = -0.0;
        return result;
    }
}