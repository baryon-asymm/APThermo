using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems.Tests;

/// <summary>
/// Bits: the front door's orchestration is a tripwire, not a contract (BOOT.md). Every rocket, tp, hp and sp fixture is solved
/// singly from its propellant, exactly as the L1 theories of <see cref="RocketTests"/> and <see cref="EquilibriumTests"/> solve
/// it, and its result is hashed with SHA-256 over the raw little-endian bits (the root BOOT.md's platform is Windows x64, always
/// little-endian) written in this order: the mixture's element moles in element order, its enthalpy and its mass, then per
/// station (chamber, throat, exits for a rocket case; the single "state" for an equilibrium case) the state, a presence flag and
/// the performance figures, a presence flag and the transport figures, the mole fractions and the condensed mass fractions in
/// the result's species order, the station status and the transport status, and finally the case status. A decomposition, a
/// regrouping of batches, a renaming or a reordering of code must not move a line of <c>Bits.approved.txt</c>; a mismatch is
/// either a defect of the refactoring or a numerical change that must be named and re-approved in the same commit.
/// </summary>
[Collection("solver")]
public sealed class BitSnapshotTests
{
    /// <summary>The path of this node's approved bit snapshot, platform-specific (root BOOT.md, Constraints).</summary>
    public static string ApprovedPath => ApprovedSnapshot.ApprovedPathFor(RepositoryPaths.Resolve("tests", "Problems.Tests"), "Bits");

    /// <summary>Every fixture gives the recorded bits.</summary>
    [Fact]
    [Trait("Category", "BitSnapshot")]
    public void EveryFixtureGivesTheRecordedBits()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var problems = new List<string>();
        var keys = new List<string>();
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var c = CeaFixtures.Load(path);
            var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
            var result = SolverFixture.Shared.Solver.Solve(propellant, FixtureCases.RocketProblemOf(c));
            Record(snapshot, problems, keys, path, HashOf(result.Mixture, result.MixtureMass, result.Species, result.Stations, result.Status));
        }

        foreach (var kind in new[] { "tp", "hp", "sp" })
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var c = CeaFixtures.Load(path);
                var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
                var result = SolverFixture.Shared.Solver.Solve(propellant, FixtureCases.EquilibriumProblemOf(c));
                Record(snapshot, problems, keys, path, HashOf(result.Mixture, result.MixtureMass, result.Species, [result.State], result.Status));
            }
        }

        foreach (var stale in snapshot.StaleKeys(keys))
        {
            problems.Add($"{stale}: recorded in Bits.approved.txt but no enumerated SolverFixture.Shared produced it");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} problem(s) with Bits.approved.txt (changed bits, missing from the file, or stale):\n" + string.Join("\n", problems.Take(20)) +
            (problems.Count > 20 ? $"\n… and {problems.Count - 20} more." : string.Empty));
    }

    /// <summary>The fixture path as the snapshot records it: relative to the repository root, forward slashes.</summary>
    private static string RelativePath(string fullPath) => Path.GetRelativePath(RepositoryPaths.Root, fullPath).Replace('\\', '/');

    /// <summary>
    /// Records one case's hash against the snapshot, passing the hash's own field trace along
    /// (<see cref="BitHash.Fields"/>): on a mismatch the harness dumps every field this case folded into the hash,
    /// beside <c>Bits.actual.txt</c>, so a run that disagrees with <c>Bits.approved.txt</c> also says which of the
    /// hashed numbers moved (BOOT.md, the Bits level's per-case field dump).
    /// </summary>
    private static void Record(ApprovedSnapshot snapshot, List<string> problems, List<string> keys, string path, BitHash hash)
    {
        var key = RelativePath(path);
        keys.Add(key);
        var problem = snapshot.Problem(key, hash.ToHex(), hash.Fields);
        if (problem is not null)
        {
            problems.Add(problem);
        }
    }

    private static BitHash HashOf(ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, CaseStatus caseStatus)
    {
        var hash = new BitHash();
        foreach (var element in mixture.Elements)
        {
            _ = hash.Add(mixture.ElementMoles[element]);
        }

        _ = hash.Add(mixture.Enthalpy!.Value);
        _ = hash.Add(mixtureMass);

        foreach (var station in stations)
        {
            WriteState(hash, station.State);

            _ = hash.Add(station.Performance.HasValue);
            if (station.Performance is { } figures)
            {
                WriteFigures(hash, figures);
            }

            _ = hash.Add(station.Transport.HasValue);
            if (station.Transport is { } transport)
            {
                WriteTransport(hash, transport);
            }

            foreach (var name in species)
            {
                _ = hash.Add(station.MoleFractions.GetValueOrDefault(name));
            }

            foreach (var name in species)
            {
                if (station.CondensedMassFractions.TryGetValue(name, out var massFraction))
                {
                    _ = hash.Add(massFraction);
                }
            }

            _ = hash.Add((int)station.Status);
            _ = hash.Add(station.TransportStatus.HasValue);
            if (station.TransportStatus is { } transportStatus)
            {
                _ = hash.Add((int)transportStatus);
            }
        }

        _ = hash.Add((int)caseStatus);
        return hash;
    }

    private static void WriteState(BitHash hash, MixtureState s) =>
        hash.Add(s.Temperature).Add(s.Pressure).Add(s.Density).Add(s.Enthalpy).Add(s.InternalEnergy).Add(s.Entropy).Add(s.GibbsEnergy)
            .Add(s.MolarMass).Add(s.MixtureMolarMass).Add(s.CpFrozen).Add(s.CpEquilibrium).Add(s.CvFrozen).Add(s.CvEquilibrium)
            .Add(s.DlnVdlnT).Add(s.DlnVdlnP).Add(s.GammaS).Add(s.SoundSpeed).Add(s.Velocity).Add(s.Mach);

    private static void WriteFigures(BitHash hash, PerformanceFigures f) =>
        hash.Add(f.AreaRatio).Add(f.PressureRatio).Add(f.CharacteristicVelocity).Add(f.ThrustCoefficient).Add(f.SpecificImpulse).Add(f.VacuumSpecificImpulse);

    private static void WriteTransport(BitHash hash, TransportFigures t) =>
        hash.Add(t.Viscosity).Add(t.FrozenConductivity).Add(t.ReactingConductivity).Add(t.FrozenPrandtl).Add(t.ReactingPrandtl)
            .Add(t.FrozenHeatCapacity).Add(t.EquilibriumHeatCapacity).Add(t.EstimatedMoleFraction).Add(t.SpeciesCount).Add(t.ReactionCount)
            .Add(t.EstimatedSpeciesCount).Add(t.TraceEliminations).Add(t.Capped);
}
