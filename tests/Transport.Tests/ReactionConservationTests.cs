using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>
/// Element conservation of the reaction basis (<see cref="ReactionBasis"/>, <see cref="ReactionSet"/>): every reaction the
/// set forms must balance every active element between its product and its components (<c>BOOT.md</c>, the ⚠ of
/// 2026-09-26, hidden-defect audit finding F4). The check reads the reaction coefficients and the species stoichiometry
/// directly, through <c>InternalsVisibleTo</c>, and is computed from the physical law rather than typed from a fixture.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ReactionConservationTests
{
    /// <summary>Conservation is exact up to the basis's own cleaning threshold; the reference offers no tolerance for it.</summary>
    private const double ConservationTolerance = 1e-8;

    /// <summary>The audit's restricted product list conserves every reaction.</summary>
    [Fact]
    public void TheAuditsRestrictedProductListConservesEveryReaction()
    {
        string[] elements = ["N", "O"];
        string[] products = ["NO2", "N2O4", "N", "O", "N2", "O2", "NO"];

        // A composition dominated by NO2 then N2O4 (what a product list without N2 gives at low temperature, audit F4): the
        // two most abundant gaseous species become the components of the N and O rows, and N2O4's column is proportional to
        // NO2's, driving the O row's pivot to zero once the N row is reduced. The trace species are well above the trace
        // fraction, so every one of the 7 products enters the set and forms a reaction.
        var moles = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["NO2"] = 0.6,
            ["N2O4"] = 0.3,
            ["N"] = 0.01,
            ["O"] = 0.01,
            ["N2"] = 0.01,
            ["O2"] = 0.01,
            ["NO"] = 0.01,
        };

        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var transport = TransportTable.Build(CpuFixture.Shared.Transport, table);
        var moleArray = new double[table.SpeciesCount];
        foreach (var (name, value) in moles)
        {
            moleArray[table.IndexOf(name)] = value;
        }

        var (status, figures, violations) = EvaluateAndCheckConservation(table, transport, moleArray, temperature: 400.0);
        Assert.Equal(CaseStatus.Ok, status);
        Assert.Equal(products.Length, figures.SpeciesCount);
        Assert.Equal(products.Length - 2, figures.ReactionCount);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// The same restricted list with the row defaults carrying no moles at all (the second hidden-defect audit's F2,
    /// 2026-09-28): the N row's component is still NO2, but N2O4's column is proportional to NO2's, so the O row's pivot
    /// vanishes once the N row is reduced and it reverts to the atomic oxygen default — which this station gives zero
    /// moles. <see cref="ComponentBasis"/> settles the revert before the set is seeded, so the default is seeded despite
    /// its zero moles (<c>BOOT.md</c>, Constraints, the ⚠ of 2026-09-28) and trace-eliminated from the reactions rather
    /// than left out of the basis entirely, which is what broke conservation before the fix.
    /// </summary>
    [Fact]
    public void TheAuditsRestrictedProductListConservesEveryReactionWhenTheRevertedDefaultCarriesNoMoles()
    {
        string[] elements = ["N", "O"];
        string[] products = ["NO2", "N2O4", "N", "O", "N2", "O2", "NO"];
        var moles = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["NO2"] = 0.6,
            ["N2O4"] = 0.3,
            ["N"] = 0.0,
            ["O"] = 0.0,
            ["N2"] = 0.01,
            ["O2"] = 0.01,
            ["NO"] = 0.01,
        };

        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var transport = TransportTable.Build(CpuFixture.Shared.Transport, table);
        var moleArray = new double[table.SpeciesCount];
        foreach (var (name, value) in moles)
        {
            moleArray[table.IndexOf(name)] = value;
        }

        var (status, figures, violations) = EvaluateAndCheckConservation(table, transport, moleArray, temperature: 400.0);
        Assert.Equal(CaseStatus.Ok, status);

        // The set carries every product but the atomic nitrogen the N row never needed (its component stayed NO2); the
        // atomic oxygen the O row reverted to is seeded with zero moles and immediately trace-eliminated.
        Assert.Equal(products.Length - 1, figures.SpeciesCount);
        Assert.Equal(1, figures.TraceEliminations);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// The audit's own six-species restricted list (2026-09-28, <c>NO2, N2O4, NO, O2, N, O</c>, no N2), solved by
    /// `Equilibrium` itself from pure N2O4's element ratio (N:O = 1:2) at 400 K, 0.1 MPa — the audit's "from N2O4(L)".
    /// Without N2 as a candidate the true equilibrium cannot collapse all the way to N2 + O2, so it stays
    /// NO2/N2O4-dominated and `Equilibrium`'s own converged state reports the same zero moles for atomic N and O that
    /// the previous fact hand-picks. Feeding that composition to `Transport` reproduces the second hidden-defect
    /// audit's own finding: the reverted default is seeded and immediately trace-eliminated, and the transport set's
    /// `EquilibriumHeatCapacity` matches `Equilibrium`'s own `CpEquilibrium` of the same state to double-precision
    /// rounding — the audit's own value, 1 998.469288525117 J/(kg·K), is not typed here; this fact reads it fresh from
    /// `Equilibrium` (<c>BOOT.md</c>, the components-settled criterion).
    /// </summary>
    [Fact]
    public void TheAuditsRestrictedListMatchesEquilibriumsHeatCapacity()
    {
        string[] elements = ["N", "O"];
        string[] products = ["NO2", "N2O4", "NO", "O2", "N", "O"];
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var transport = TransportTable.Build(CpuFixture.Shared.Transport, table);

        // Pure N2O4 by element ratio, normalised so the element masses sum to 1 kg of mixture: the molar mass comes
        // from the table's own data (the root's "no coefficient typed into code" invariant), the atom counts (2, 4)
        // from N2O4's formula. `table.Elements` is ["N", "O"], the order `elements` above was given in.
        var n2O4MolarMass = table.Arrays.MolarMass[table.IndexOf("N2O4")];
        var elementMoles = new double[table.ElementCount];
        elementMoles[0] = 2.0 / n2O4MolarMass;
        elementMoles[1] = 4.0 / n2O4MolarMass;

        var equilibrium = EquilibriumHost.SolveTp(CpuFixture.Shared.Accelerator, table, elementMoles, temperature: 400.0, pressure: 1e5);
        Assert.Equal(CaseStatus.Ok, equilibrium.Status);

        var (status, figures, violations) = EvaluateAndCheckConservation(table, transport, equilibrium.Moles, temperature: 400.0);
        Assert.Equal(CaseStatus.Ok, status);
        Assert.True(violations.Count == 0, string.Join("\n", violations));

        // The reverted default carries the zero moles Equilibrium itself converges to, and is trace-eliminated at once.
        Assert.Equal(1, figures.TraceEliminations);
        var relativeDifference = Math.Abs(figures.EquilibriumHeatCapacity - equilibrium.State.CpEquilibrium) / equilibrium.State.CpEquilibrium;
        Assert.True(relativeDifference < 1e-9,
            $"transport {figures.EquilibriumHeatCapacity:R} vs equilibrium {equilibrium.State.CpEquilibrium:R} ({relativeDifference:E} relative)");
    }

    /// <summary>Every reaction of every station's set, over every rocket fixture run with transport, conserves the elements.</summary>
    [Fact]
    public void EveryReactionOfEveryStationsSetConservesTheElements()
    {
        var names = TransportHost.RocketCaseNamesWithTransport().ToList();
        Assert.NotEmpty(names);
        var violations = new List<string>();
        var stationsSeen = 0;
        foreach (var name in names)
        {
            var c = TransportHost.LoadRocket(name);
            var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
            foreach (var station in TransportHost.StationsWithTransport(c))
            {
                stationsSeen++;
                var label = name + " " + station.GetProperty("station").GetString();
                var temperature = station.GetProperty("temperature").GetDouble();
                var moles = TransportHost.MolesOf(table, station);
                var (status, _, stationViolations) = EvaluateAndCheckConservation(table, transport, moles, temperature);
                if (status != CaseStatus.Ok)
                {
                    violations.Add($"{label}: status {status}");
                    continue;
                }

                violations.AddRange(stationViolations.Select(v => $"{label} {v}"));
            }
        }

        Assert.True(stationsSeen > 0);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Runs the solver on the host and checks the conservation of every reaction the set formed. Shared with
    /// <see cref="EquilibriumConsistencyTests"/>, which cross-checks the same figures against an independently solved
    /// state, so the conservation check is written once.</summary>
    internal static (CaseStatus Status, TransportFigures Figures, IReadOnlyList<string> Violations) EvaluateAndCheckConservation(
        SpeciesTable table, TransportTable transport, double[] moles, double temperature)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(accelerator, transport);
        using var molesBuffer = accelerator.Allocate1D(moles);
        using var doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(elementCount));
        using var ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figuresBuffer = accelerator.Allocate1D<TransportFigures>(1);
        var scratch = TransportScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var speciesView = speciesBuffers.View;
        var transportView = transportBuffers.View;
        var status = TransportSolver.Evaluate(in speciesView, in transportView, temperature, molesBuffer.View, in scratch, figuresBuffer.View);
        var figures = figuresBuffer.GetAsArray1D()[0];
        if (status != CaseStatus.Ok)
        {
            return (status, figures, []);
        }

        var violations = Conservation(table, scratch, figures.SpeciesCount, figures.ReactionCount);
        return (status, figures, violations);
    }

    /// <summary>
    /// For every reaction of the set, the stoichiometry it forms from the components must vanish over every active element:
    /// Σ_a Alpha[r, a] · Stoichiometry[element, IndexList[a]], over the species of the set.
    /// </summary>
    private static List<string> Conservation(SpeciesTable table, TransportScratch scratch, int nm, int nr)
    {
        const int stride = TransportSolver.Stride;
        var speciesCount = table.SpeciesCount;
        var stoichiometry = table.Arrays.Stoichiometry;
        var violations = new List<string>();
        for (var r = 0; r < nr; r++)
        {
            for (var i = 0; i < table.ElementCount; i++)
            {
                if (scratch.RowActive[i] == 0)
                {
                    continue;
                }

                var sum = 0.0;
                for (var a = 0; a < nm; a++)
                {
                    sum += scratch.Alpha[r * stride + a] * stoichiometry[i * speciesCount + scratch.IndexList[a]];
                }

                if (!(Math.Abs(sum) <= ConservationTolerance))
                {
                    violations.Add($"reaction {r} element {table.Elements[i]}: conservation residual {sum:R}");
                }
            }
        }

        return violations;
    }
}
