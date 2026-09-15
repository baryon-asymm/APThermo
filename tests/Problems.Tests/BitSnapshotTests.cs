using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems.Tests;

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
[Collection(SolverCollection.Name)]
public sealed class BitSnapshotTests(SolverFixture fixture)
{
    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Problems.Tests", "Bits.approved.txt");

    [Fact]
    public void Every_fixture_gives_the_recorded_bits()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var problems = new List<string>();
        var keys = new List<string>();
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var c = CeaFixtures.Load(path);
            var propellant = FixtureCases.PropellantOf(fixture.Database, c);
            var result = fixture.Solver.Solve(propellant, FixtureCases.RocketProblemOf(c));
            Record(snapshot, problems, keys, path, HashOf(result.Mixture, result.MixtureMass, result.Species, result.Stations, result.Status));
        }

        foreach (var kind in new[] { "tp", "hp", "sp" })
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var c = CeaFixtures.Load(path);
                var propellant = FixtureCases.PropellantOf(fixture.Database, c);
                var result = fixture.Solver.Solve(propellant, FixtureCases.EquilibriumProblemOf(c));
                Record(snapshot, problems, keys, path, HashOf(result.Mixture, result.MixtureMass, result.Species, [result.State], result.Status));
            }
        }

        foreach (var stale in snapshot.StaleKeys(keys))
        {
            problems.Add($"{stale}: recorded in Bits.approved.txt but no enumerated fixture produced it");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} problem(s) with Bits.approved.txt (changed bits, missing from the file, or stale):\n" + string.Join("\n", problems.Take(20)) +
            (problems.Count > 20 ? $"\n… and {problems.Count - 20} more." : string.Empty));
    }

    /// <summary>The fixture path as the snapshot records it: relative to the repository root, forward slashes.</summary>
    private static string RelativePath(string fullPath) => Path.GetRelativePath(RepositoryPaths.Root, fullPath).Replace('\\', '/');

    private static void Record(ApprovedSnapshot snapshot, List<string> problems, List<string> keys, string path, string hash)
    {
        var key = RelativePath(path);
        keys.Add(key);
        var problem = snapshot.Problem(key, hash);
        if (problem is not null)
        {
            problems.Add(problem);
        }
    }

    private static string HashOf(ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, CaseStatus caseStatus)
    {
        var hash = new BitHash();
        foreach (var element in mixture.Elements)
        {
            hash.Add(mixture.ElementMoles[element]);
        }

        hash.Add(mixture.Enthalpy!.Value);
        hash.Add(mixtureMass);

        foreach (var station in stations)
        {
            WriteState(hash, station.State);

            hash.Add(station.Performance.HasValue);
            if (station.Performance is { } figures)
            {
                WriteFigures(hash, figures);
            }

            hash.Add(station.Transport.HasValue);
            if (station.Transport is { } transport)
            {
                WriteTransport(hash, transport);
            }

            foreach (var name in species)
            {
                hash.Add(station.MoleFractions.GetValueOrDefault(name));
            }

            foreach (var name in species)
            {
                if (station.CondensedMassFractions.TryGetValue(name, out var massFraction))
                {
                    hash.Add(massFraction);
                }
            }

            hash.Add((int)station.Status);
            hash.Add(station.TransportStatus.HasValue);
            if (station.TransportStatus is { } transportStatus)
            {
                hash.Add((int)transportStatus);
            }
        }

        hash.Add((int)caseStatus);
        return hash.ToHex();
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
