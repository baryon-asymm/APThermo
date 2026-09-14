using AerospacePropellantThermodynamics.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>
/// The one status no composition of a real table reaches: a reaction system that cannot be solved. The stage is driven directly
/// (the node's InternalsVisibleTo) over a set of three species and two reactions whose second system is singular while the first
/// is not, which is the case the contract speaks of: the frozen figures are written and the reacting ones equal them.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class StatusTests(CpuFixture fixture)
{
    private const int SetSpecies = 3;
    private const int SetReactions = 2;

    /// <summary>Mole fractions of the three species of the set, all far above the trace fraction.</summary>
    private static readonly double[] MoleFractions = [0.5, 0.3, 0.2];

    /// <summary>H°/RT of the three, chosen so that neither reaction has a zero enthalpy difference.</summary>
    private static readonly double[] Enthalpies = [1.0, 2.0, 4.0];

    /// <summary>Cp°/R of the three: any positive numbers; they decide the frozen heat capacity only.</summary>
    private static readonly double[] HeatCapacities = [3.5, 4.0, 4.5];

    /// <summary>
    /// kg/kmol. The third species weighs nothing, so RT/(pD) vanishes for both pairs that hold it: the second system keeps one
    /// pair, whose difference vector is the same for both reactions, and is exactly rank one. The first system keeps all three.
    /// </summary>
    private static readonly double[] SingularMasses = [2.0, 18.0, 0.0];

    /// <summary>The same set with a mass for the third species: every pair carries a positive weight and both systems are solved.</summary>
    private static readonly double[] SolvableMasses = [2.0, 18.0, 28.0];

    [Fact]
    public void A_reaction_system_that_cannot_be_solved_keeps_the_frozen_figures()
    {
        var mixture = new MixtureTransport(8.5e-5, 0.42);
        var reaction = Contribution(SingularMasses);
        Assert.Equal(CaseStatus.SingularMatrix, reaction.Status);
        var figures = default(TransportFigures);
        Fill(SingularMasses, in mixture, in reaction, ref figures);
        Assert.True(figures.ReactingConductivity == figures.FrozenConductivity,
                    $"reacting conductivity {figures.ReactingConductivity:R} against the frozen {figures.FrozenConductivity:R}");
        Assert.True(figures.EquilibriumHeatCapacity == figures.FrozenHeatCapacity,
                    $"equilibrium heat capacity {figures.EquilibriumHeatCapacity:R} against the frozen {figures.FrozenHeatCapacity:R}");
        Assert.True(figures.ReactingPrandtl == figures.FrozenPrandtl,
                    $"reacting Prandtl {figures.ReactingPrandtl:R} against the frozen {figures.FrozenPrandtl:R}");
    }

    [Fact]
    public void The_same_set_is_solved_when_every_pair_carries_a_diffusion_weight()
    {
        var reaction = Contribution(SolvableMasses);
        Assert.Equal(CaseStatus.Ok, reaction.Status);
        Assert.True(reaction.HeatCapacity > 0.0, $"the reaction heat capacity {reaction.HeatCapacity:R} is not positive");
        Assert.True(reaction.Conductivity > 0.0, $"the reaction conductivity {reaction.Conductivity:R} is not positive");
    }

    /// <summary>Runs the reaction-terms stage over the synthetic set with the given molar masses.</summary>
    private ReactionContribution Contribution(double[] masses)
    {
        using var buffers = new SetBuffers(fixture.Accelerator, masses);
        var inputs = buffers.Inputs;
        return ReactionTerms.Evaluate(in inputs, SetSpecies, SetReactions);
    }

    /// <summary>Fills the figures of the synthetic set from the mixture rules and the reaction contribution.</summary>
    private void Fill(double[] masses, in MixtureTransport mixture, in ReactionContribution reaction, ref TransportFigures figures)
    {
        using var buffers = new SetBuffers(fixture.Accelerator, masses);
        var inputs = buffers.Inputs;
        SetProperties.Fill(in inputs, SetSpecies, in mixture, in reaction, ref figures);
    }

    /// <summary>
    /// The buffers of one synthetic set: three species of the given masses, two reactions (H2 ⇌ … with the third species in the
    /// second one only) and a finite interaction viscosity for every pair.
    /// </summary>
    private sealed class SetBuffers : IDisposable
    {
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _doubles;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _ints;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _molarMass;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _spare;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _spareInts;

        public SetBuffers(Accelerator accelerator, double[] masses)
        {
            _doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(SetSpecies, 1));
            _ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(SetSpecies, 1));
            _molarMass = accelerator.Allocate1D(masses);
            _spare = accelerator.Allocate1D<double>(SetSpecies * SetSpecies);
            _spareInts = accelerator.Allocate1D<int>(SetSpecies * SetSpecies);
            var scratch = TransportScratch.Slice(_doubles.View, _ints.View, SetSpecies, 1);
            Prepare(scratch);
            var species = new SpeciesTableView(SetSpecies, SetSpecies, 1, _molarMass.View, _spare.View, _spare.View,
                                               _spareInts.View, _spareInts.View, _spare.View, _spare.View, _spare.View);
            var transport = new TransportTableView(SetSpecies, 0, _spareInts.View, _spareInts.View, _spareInts.View, _spareInts.View,
                                                   _spare.View, _spareInts.View, _spareInts.View, _spareInts.View);
            Inputs = new StationInputs(in species, in transport, in scratch, _spare.View, 3000.0);
        }

        public StationInputs Inputs { get; }

        public void Dispose()
        {
            _doubles.Dispose();
            _ints.Dispose();
            _molarMass.Dispose();
            _spare.Dispose();
            _spareInts.Dispose();
        }

        /// <summary>The set as the earlier stages would have left it: the species of the set, their data, and the two reactions.</summary>
        private static void Prepare(TransportScratch scratch)
        {
            var stride = TransportSolver.Stride;
            for (var a = 0; a < SetSpecies; a++)
            {
                scratch.IndexList[a] = a;
                scratch.Xs[a] = MoleFractions[a];
                scratch.H[a] = Enthalpies[a];
                scratch.Cp[a] = HeatCapacities[a];
                for (var b = 0; b < SetSpecies; b++)
                {
                    scratch.Eta[a * stride + b] = 1e-5;
                }
            }

            // Both reactions form species 0 and 1 alike, so the pair (0, 1) gives them one and the same difference vector; only
            // the second reaction holds species 2, so the pairs that hold it are what makes the first system non-singular.
            double[] first = [1.0, -1.0, 0.0];
            double[] second = [1.0, -1.0, 1.0];
            for (var b = 0; b < SetSpecies; b++)
            {
                scratch.Alpha[b] = first[b];
                scratch.Alpha[stride + b] = second[b];
            }
        }
    }
}
