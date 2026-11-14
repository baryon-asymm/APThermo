using System.Runtime.CompilerServices;
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
    public static string ApprovedPath => ApprovedSnapshot.RecordPathFor(RepositoryPaths.Resolve("tests", "Problems.Tests"), "Bits");

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
            Record(snapshot, problems, keys, path, c, HashOf(result.Mixture, result.MixtureMass, result.Species, result.Stations, result.Status));
        }

        foreach (var kind in new[] { "tp", "hp", "sp" })
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var c = CeaFixtures.Load(path);
                var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
                var result = SolverFixture.Shared.Solver.Solve(propellant, FixtureCases.EquilibriumProblemOf(c));
                Record(snapshot, problems, keys, path, c, HashOf(result.Mixture, result.MixtureMass, result.Species, [result.State], result.Status));
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
    private static void Record(ApprovedSnapshot snapshot, List<string> problems, List<string> keys, string path, CeaCase fixture,
                               (BitHash Hash, IReadOnlyList<string> Fields) actual)
    {
        var key = RelativePath(path);
        keys.Add(key);
        var fields = actual.Fields.Concat(fixture.Inputs.EnumerateObject()
            .Select(property => $"inputs.{property.Name} = {property.Value.GetRawText()}")).ToArray();
        var problem = snapshot.Problem(key, actual.Hash.ToHex(), fields);
        if (problem is not null)
        {
            problems.Add(problem);
        }
    }

    private static (BitHash Hash, IReadOnlyList<string> Fields) HashOf(
        ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, CaseStatus caseStatus)
    {
        var hash = new BitHash();
        var names = new List<string>();
        foreach (var element in mixture.Elements)
        {
            _ = hash.Add(mixture.ElementMoles[element]);
            names.Add($"mixture.elementMoles[{element}]");
        }

        _ = hash.Add(mixture.Enthalpy!.Value);
        names.Add("mixture.enthalpy");
        _ = hash.Add(mixtureMass);
        names.Add("mixture.mass");

        var index = 0;
        foreach (var station in stations)
        {
            WriteStation(hash, names, species, station, index);
            index++;
        }

        _ = hash.Add((int)caseStatus);
        names.Add($"case.status ({caseStatus})");
        Assert.Equal(hash.Fields.Count, names.Count);
        return (hash, names.Zip(hash.Fields, (name, value) => $"{name} = {value}").ToArray());
    }

    private static void WriteStation(BitHash hash, List<string> names, IReadOnlyList<string> species, Station station, int index)
    {
        var prefix = $"stations[{index}] ({station.Name})";
        WriteState(hash, names, prefix + ".state", station.State);

        _ = hash.Add(station.Performance.HasValue);
        names.Add(prefix + ".performance.present");
        if (station.Performance is { } figures)
        {
            WriteFigures(hash, names, prefix + ".performance", figures);
        }

        _ = hash.Add(station.Transport.HasValue);
        names.Add(prefix + ".transport.present");
        if (station.Transport is { } transport)
        {
            WriteTransport(hash, names, prefix + ".transport", transport);
        }

        foreach (var name in species)
        {
            _ = hash.Add(station.MoleFractions.GetValueOrDefault(name));
            names.Add($"{prefix}.moleFractions[{name}]");
        }

        foreach (var name in species)
        {
            if (station.CondensedMassFractions.TryGetValue(name, out var massFraction))
            {
                _ = hash.Add(massFraction);
                names.Add($"{prefix}.condensedMassFractions[{name}]");
            }
        }

        _ = hash.Add((int)station.Status);
        names.Add($"{prefix}.status ({station.Status})");
        _ = hash.Add(station.TransportStatus.HasValue);
        names.Add(prefix + ".transportStatus.present");
        if (station.TransportStatus is { } transportStatus)
        {
            _ = hash.Add((int)transportStatus);
            names.Add($"{prefix}.transportStatus ({transportStatus})");
        }
    }

    private static void WriteState(BitHash hash, List<string> names, string prefix, MixtureState s)
    {
        WriteProperty(hash, names, prefix, s.Temperature);
        WriteProperty(hash, names, prefix, s.Pressure);
        WriteProperty(hash, names, prefix, s.Density);
        WriteProperty(hash, names, prefix, s.Enthalpy);
        WriteProperty(hash, names, prefix, s.InternalEnergy);
        WriteProperty(hash, names, prefix, s.Entropy);
        WriteProperty(hash, names, prefix, s.GibbsEnergy);
        WriteProperty(hash, names, prefix, s.MolarMass);
        WriteProperty(hash, names, prefix, s.MixtureMolarMass);
        WriteProperty(hash, names, prefix, s.CpFrozen);
        WriteProperty(hash, names, prefix, s.CpEquilibrium);
        WriteProperty(hash, names, prefix, s.CvFrozen);
        WriteProperty(hash, names, prefix, s.CvEquilibrium);
        WriteProperty(hash, names, prefix, s.DlnVdlnT);
        WriteProperty(hash, names, prefix, s.DlnVdlnP);
        WriteProperty(hash, names, prefix, s.GammaS);
        WriteProperty(hash, names, prefix, s.SoundSpeed);
        WriteProperty(hash, names, prefix, s.Velocity);
        WriteProperty(hash, names, prefix, s.Mach);
    }

    private static void WriteFigures(BitHash hash, List<string> names, string prefix, PerformanceFigures f)
    {
        WriteProperty(hash, names, prefix, f.AreaRatio);
        WriteProperty(hash, names, prefix, f.PressureRatio);
        WriteProperty(hash, names, prefix, f.CharacteristicVelocity);
        WriteProperty(hash, names, prefix, f.ThrustCoefficient);
        WriteProperty(hash, names, prefix, f.SpecificImpulse);
        WriteProperty(hash, names, prefix, f.VacuumSpecificImpulse);
    }

    private static void WriteTransport(BitHash hash, List<string> names, string prefix, TransportFigures t)
    {
        WriteProperty(hash, names, prefix, t.Viscosity);
        WriteProperty(hash, names, prefix, t.FrozenConductivity);
        WriteProperty(hash, names, prefix, t.ReactingConductivity);
        WriteProperty(hash, names, prefix, t.FrozenPrandtl);
        WriteProperty(hash, names, prefix, t.ReactingPrandtl);
        WriteProperty(hash, names, prefix, t.FrozenHeatCapacity);
        WriteProperty(hash, names, prefix, t.EquilibriumHeatCapacity);
        WriteProperty(hash, names, prefix, t.EstimatedMoleFraction);
        WriteProperty(hash, names, prefix, t.SpeciesCount);
        WriteProperty(hash, names, prefix, t.ReactionCount);
        WriteProperty(hash, names, prefix, t.EstimatedSpeciesCount);
        WriteProperty(hash, names, prefix, t.TraceEliminations);
        WriteProperty(hash, names, prefix, t.Capped);
    }

    private static void WriteProperty(BitHash hash, List<string> names, string prefix, double value,
                                      [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        _ = hash.Add(value);
        names.Add(prefix + "." + expression[(expression.IndexOf('.') + 1)..]);
    }

    private static void WriteProperty(BitHash hash, List<string> names, string prefix, int value,
                                      [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        _ = hash.Add(value);
        names.Add(prefix + "." + expression[(expression.IndexOf('.') + 1)..]);
    }
}