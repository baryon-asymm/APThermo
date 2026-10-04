using APThermo.Equilibrium;
using APThermo.Execution.Chunks;
using APThermo.Performance;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the 32-bit offset cap is proven as wiring (BOOT.md, `Execution.Chunks`, 2026-09-28, the guards audit's F8), through
/// each pipeline's own real buffer declarations (<see cref="EquilibriumPipeline.DeclareBuffers"/> and its two mirrors), not
/// only <see cref="ChunkPlan.For(int, long, long, EngineOptions)"/> called with explicit numbers, which the pre-existing
/// <c>AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits</c> already covers.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ChunkPlanWiringTests
{
    private const int SpeciesCount = 900;
    private const int ElementCount = 40;
    private const int Exits = 3;

    private static readonly EngineOptions Huge = new() { Accelerator = AcceleratorKind.Cpu, ChunkSize = int.MaxValue, ScratchBytes = 64L << 30 };

    /// <summary>
    /// Each pipeline's real declared buffers cap a plan of a huge synthetic count so that <c>Size × the largest declared
    /// per-case stride</c> stays within a 32-bit offset. The expected stride is computed independently in
    /// <see cref="Pipelines"/>, from the same layout functions the pipeline itself calls (<c>ScratchLayout</c>,
    /// <c>RocketLayout</c>, <c>TransportLayout</c>) and the element/species counts given to <c>DeclareBuffers</c> —
    /// never by reading <see cref="ChunkBuffers.MaxElementsPerCase"/> back, so the fact cannot pass merely because it
    /// and the pipeline agree on a broken value. Red once: with <c>ChunkBuffers.MaxElementsPerCase</c> mutated to
    /// always answer zero, <see cref="ChunkPlan.For(int, long, long, EngineOptions)"/> applies no offset cap at all
    /// and every plan's <see cref="ChunkPlan.Size"/> grows to <see cref="EngineOptions.ChunkSize"/> itself, far above
    /// the independent bound below (confirmed by temporarily reading <c>0L</c> in place of the real computation while
    /// writing this fact).
    /// </summary>
    [Theory]
    [MemberData(nameof(Pipelines))]
    public void EachPipelinesChosenPlanRespectsItsOwnOffsetCap(string name, long expectedLargestStride)
    {
        var accelerator = EngineFixture.Shared.Cpu.IlgpuAccelerator;
        using var buffers = name switch
        {
            "Equilibrium" => EquilibriumPipeline.DeclareBuffers(accelerator, SpeciesCount, ElementCount),
            "Rocket" => RocketPipeline.DeclareBuffers(accelerator, SpeciesCount, ElementCount, Exits),
            "Transport" => TransportPipeline.DeclareBuffers(accelerator, SpeciesCount, ElementCount),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such pipeline"),
        };
        var plan = ChunkPlan.For(1_000_000_000, buffers.BytesPerCase, buffers.MaxElementsPerCase, Huge);
        Assert.True(plan.Size * expectedLargestStride <= int.MaxValue,
                    $"{name}: {plan.Size} * {expectedLargestStride} (independently expected) overflows a 32-bit offset");
    }

    /// <summary>
    /// A seeded equilibrium batch declares the same device bytes per case and the same largest per-case element count as a cold one
    /// (2026-10-04): its moles buffer is an <c>InputOutput</c> of the stride the <c>ClearedOutput</c> it replaces had, so a plan's chunk size
    /// does not move. Red once, 2026-10-04: with <c>InputOutput</c>'s <c>BytesPerCase</c> answering zero the two differ by
    /// <c>SpeciesCount × 8</c>.
    /// </summary>
    [Fact]
    public void ASeededEquilibriumBatchDeclaresTheSameDeviceBytesAsAColdOne()
    {
        var accelerator = EngineFixture.Shared.Cpu.IlgpuAccelerator;
        using var cold = EquilibriumPipeline.DeclareBuffers(accelerator, SpeciesCount, ElementCount, seeded: false);
        using var seeded = EquilibriumPipeline.DeclareBuffers(accelerator, SpeciesCount, ElementCount, seeded: true);
        Assert.Equal(cold.BytesPerCase, seeded.BytesPerCase);
        Assert.Equal(cold.MaxElementsPerCase, seeded.MaxElementsPerCase);
    }

    /// <summary>The three pipelines whose per-case buffers actually scale with the element or species count, each with
    /// its independently computed largest declared stride (the species-function pipeline's buffers are all one
    /// element per case, so the offset cap is vacuous there and it is not included).</summary>
    public static TheoryData<string, long> Pipelines()
    {
        var equilibriumStride = Math.Max(ElementCount, Math.Max(SpeciesCount,
            Math.Max(ScratchLayout.DoublesPerCase(SpeciesCount, ElementCount), ScratchLayout.IntsPerCase(SpeciesCount, ElementCount))));
        var stations = RocketLayout.StationCount(Exits);
        var rocketStride = Math.Max(ElementCount, Math.Max((long)stations * SpeciesCount, Math.Max((long)stations * ElementCount,
            Math.Max(ScratchLayout.DoublesPerCase(SpeciesCount, ElementCount), ScratchLayout.IntsPerCase(SpeciesCount, ElementCount)))));
        var transportStride = Math.Max(SpeciesCount,
            Math.Max(TransportLayout.DoublesPerCase(ElementCount), TransportLayout.IntsPerCase(SpeciesCount, ElementCount)));

        return new TheoryData<string, long>
        {
            { "Equilibrium", equilibriumStride },
            { "Rocket", rocketStride },
            { "Transport", transportStride },
        };
    }
}
