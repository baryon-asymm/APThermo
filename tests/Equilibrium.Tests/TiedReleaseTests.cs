using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The third audit pass's finding F1 (BOOT.md, "Release", 2026-09-28): the six salt states named in the ⚠ under
/// rule A's release, on the committed <c>kclo4_T500</c>/<c>naclo4_T500</c> fixtures' own tables (the same candidate
/// list at every temperature the fixtures node's generator produced for either salt, so it serves every state below
/// too). At each, the tie a Newton step ties (K or Na against Cl, through <c>KCL(cr)</c>/<c>NaCL(cr)</c>) converges
/// and polishes, the release then finds the trace carriers (<c>K</c>/<c>Cl</c>/<c>KO</c> or their sodium
/// equivalents) tell the pair apart once the second-stage retention threshold admits them, and the settled set's own
/// re-convergence without the tie oscillates those same carriers across that threshold on every step until the cap:
/// <c>NotConverged</c> at `c02e14d`, where the way back restores the tied, already-polished iterate and closes with
/// the tie in force.
///
/// cea 3.3.4 (the fixtures node's `cea_verdict.py`-style probe over the same pure-element reactants, run once and
/// recorded here rather than committed, since these six states are inputs on an existing table, not fixture files)
/// converges every one of the six: KClO4 930 K/0.1 bar (G = -7071326.69 J/kg), 980 K/1 bar (-7042071.18),
/// 990 K/1 bar (-7087927.17), 1080 K/10 bar (-7212999.22), 1070 K/70 bar (-6917312.57); NaClO4 1050 K/1 bar
/// (-8035940.49) — each within 1e-5 relative of this node's own `G` at the same state after the fix, `O2`/`KCL(cr)`
/// or `O2`/`NaCL(cr)` at the salt's 2:1 mole ratio in every case, matching the "release disabled" counterfactual the
/// audit itself measured (same composition to the printed digits, iteration count only slightly higher with the
/// release's one extra Newton call).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TiedReleaseTests
{
    /// <summary>The mole-fraction-weighted chemical-potential residual a retained gas may show against Σ a_ij π_i (the audit's own bound).</summary>
    private const double GasChemicalPotentialResidual = 1e-6;

    /// <summary>The fixture (kind tp), temperature and pressure of each of the six salt states.</summary>
    public static TheoryData<string, double, double> SixSaltStates() => new()
    {
        { "kclo4_T500", 930.0, 0.1e5 },
        { "kclo4_T500", 980.0, 1.0e5 },
        { "kclo4_T500", 990.0, 1.0e5 },
        { "kclo4_T500", 1080.0, 10.0e5 },
        { "kclo4_T500", 1070.0, 70.0e5 },
        { "naclo4_T500", 1050.0, 1.0e5 },
    };

    /// <summary>Every one of the six states ends Ok and clears every independent equilibrium condition, where `c02e14d` reported NotConverged.</summary>
    [Theory]
    [MemberData(nameof(SixSaltStates))]
    public void TheReleasedTieRestoresAndClosesOk(string fixtureName, double temperature, double pressure)
    {
        var c = HostSolver.Load("tp", fixtureName);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, elementMoles);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);

        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Empty(EquilibriumConditions.Violations(solution, GasChemicalPotentialResidual));
    }
}
