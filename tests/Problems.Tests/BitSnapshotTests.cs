using System.Security.Cryptography;
using AerospacePropellantThermodynamics.Fixtures;
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
    [Fact]
    public void Every_fixture_gives_the_recorded_bits()
    {
        var actual = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var c = CeaFixtures.Load(path);
            var propellant = FixtureCases.PropellantOf(fixture.Database, c);
            var result = fixture.Solver.Solve(propellant, FixtureCases.RocketProblemOf(c));
            actual[RelativePath(path)] = HashOf(result.Mixture, result.MixtureMass, result.Species, result.Stations, result.Status);
        }

        foreach (var kind in new[] { "tp", "hp", "sp" })
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var c = CeaFixtures.Load(path);
                var propellant = FixtureCases.PropellantOf(fixture.Database, c);
                var result = fixture.Solver.Solve(propellant, FixtureCases.EquilibriumProblemOf(c));
                actual[RelativePath(path)] = HashOf(result.Mixture, result.MixtureMass, result.Species, [result.State], result.Status);
            }
        }

        CompareWithApproved(actual);
    }

    /// <summary>The fixture path as the snapshot records it: relative to the repository root, forward slashes.</summary>
    private static string RelativePath(string fullPath) => Path.GetRelativePath(RepositoryPaths.Root, fullPath).Replace('\\', '/');

    /// <summary>
    /// Reads <c>Bits.approved.txt</c>, compares every recomputed hash against it, and fails naming every fixture that changed
    /// or is missing, after writing the full recomputed snapshot to <c>Bits.actual.txt</c> (git-ignored) so that an intended
    /// change can be reviewed and copied over the approved file in the same commit that explains it.
    /// </summary>
    private static void CompareWithApproved(IReadOnlyDictionary<string, string> actual)
    {
        var approvedPath = RepositoryPaths.Resolve("tests", "Problems.Tests", "Bits.approved.txt");
        var actualPath = RepositoryPaths.Resolve("tests", "Problems.Tests", "Bits.actual.txt");
        var approved = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(approvedPath))
        {
            foreach (var line in File.ReadAllLines(approvedPath))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split(' ', 2);
                approved[parts[0]] = parts[1];
            }
        }

        var mismatches = new List<string>();
        foreach (var (path, hash) in actual)
        {
            if (!approved.TryGetValue(path, out var expected))
            {
                mismatches.Add($"{path}: not in Bits.approved.txt");
            }
            else if (!string.Equals(expected, hash, StringComparison.Ordinal))
            {
                mismatches.Add($"{path}: bits differ from Bits.approved.txt");
            }
        }

        if (mismatches.Count > 0)
        {
            File.WriteAllLines(actualPath, actual.Select(kv => $"{kv.Key} {kv.Value}"));
        }
        else if (File.Exists(actualPath))
        {
            File.Delete(actualPath);
        }

        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} fixture(s) changed bits or are missing from Bits.approved.txt: " +
            string.Join("; ", mismatches.Take(20)) + (mismatches.Count > 20 ? "; …" : string.Empty) +
            ". Bits.actual.txt was written next to Bits.approved.txt with the full recomputed snapshot (BOOT.md, Bits level); " +
            "if every change is an intended numerical change, name it and copy Bits.actual.txt over Bits.approved.txt in the same commit.");
    }

    private static string HashOf(ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, CaseStatus caseStatus)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (var element in mixture.Elements)
            {
                writer.Write(mixture.ElementMoles[element]);
            }

            writer.Write(mixture.Enthalpy!.Value);
            writer.Write(mixtureMass);

            foreach (var station in stations)
            {
                WriteState(writer, station.State);

                writer.Write(station.Performance.HasValue);
                if (station.Performance is { } figures)
                {
                    WriteFigures(writer, figures);
                }

                writer.Write(station.Transport.HasValue);
                if (station.Transport is { } transport)
                {
                    WriteTransport(writer, transport);
                }

                foreach (var name in species)
                {
                    writer.Write(station.MoleFractions.GetValueOrDefault(name));
                }

                foreach (var name in species)
                {
                    if (station.CondensedMassFractions.TryGetValue(name, out var massFraction))
                    {
                        writer.Write(massFraction);
                    }
                }

                writer.Write((int)station.Status);
                writer.Write(station.TransportStatus.HasValue);
                if (station.TransportStatus is { } transportStatus)
                {
                    writer.Write((int)transportStatus);
                }
            }

            writer.Write((int)caseStatus);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteState(BinaryWriter writer, MixtureState s)
    {
        writer.Write(s.Temperature);
        writer.Write(s.Pressure);
        writer.Write(s.Density);
        writer.Write(s.Enthalpy);
        writer.Write(s.InternalEnergy);
        writer.Write(s.Entropy);
        writer.Write(s.GibbsEnergy);
        writer.Write(s.MolarMass);
        writer.Write(s.MixtureMolarMass);
        writer.Write(s.CpFrozen);
        writer.Write(s.CpEquilibrium);
        writer.Write(s.CvFrozen);
        writer.Write(s.CvEquilibrium);
        writer.Write(s.DlnVdlnT);
        writer.Write(s.DlnVdlnP);
        writer.Write(s.GammaS);
        writer.Write(s.SoundSpeed);
        writer.Write(s.Velocity);
        writer.Write(s.Mach);
    }

    private static void WriteFigures(BinaryWriter writer, PerformanceFigures f)
    {
        writer.Write(f.AreaRatio);
        writer.Write(f.PressureRatio);
        writer.Write(f.CharacteristicVelocity);
        writer.Write(f.ThrustCoefficient);
        writer.Write(f.SpecificImpulse);
        writer.Write(f.VacuumSpecificImpulse);
    }

    private static void WriteTransport(BinaryWriter writer, TransportFigures t)
    {
        writer.Write(t.Viscosity);
        writer.Write(t.FrozenConductivity);
        writer.Write(t.ReactingConductivity);
        writer.Write(t.FrozenPrandtl);
        writer.Write(t.ReactingPrandtl);
        writer.Write(t.FrozenHeatCapacity);
        writer.Write(t.EquilibriumHeatCapacity);
        writer.Write(t.EstimatedMoleFraction);
        writer.Write(t.SpeciesCount);
        writer.Write(t.ReactionCount);
        writer.Write(t.EstimatedSpeciesCount);
        writer.Write(t.TraceEliminations);
        writer.Write(t.Capped);
    }
}
