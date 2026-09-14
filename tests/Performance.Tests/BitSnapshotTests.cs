using System.Reflection;
using System.Security.Cryptography;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>
/// The raw bits of one host solve, hashed: every station's <see cref="MixtureState"/> fields in declaration order, then every
/// station's moles, multipliers and <see cref="PerformanceFigures"/> fields, the station statuses, the iteration counts and the
/// case status. The fields are enumerated by reflection, so a new field enters the hash without a change here.
/// </summary>
internal static class RocketBits
{
    private static readonly FieldInfo[] StateFields = Declared(typeof(MixtureState));

    private static readonly FieldInfo[] FigureFields = Declared(typeof(PerformanceFigures));

    public static string Hash(RocketSolution solution)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var speciesCount = solution.Table.SpeciesCount;
        var elementCount = solution.Table.ElementCount;
        for (var station = 0; station < solution.StationCount; station++)
        {
            var state = solution.Stations[station];
            foreach (var field in StateFields)
            {
                Append(hash, (double)field.GetValue(state)!);
            }
        }

        for (var station = 0; station < solution.StationCount; station++)
        {
            for (var j = 0; j < speciesCount; j++)
            {
                Append(hash, solution.Moles[station * speciesCount + j]);
            }

            for (var i = 0; i < elementCount; i++)
            {
                Append(hash, solution.Multipliers[station * elementCount + i]);
            }

            var figures = solution.Figures[station];
            foreach (var field in FigureFields)
            {
                Append(hash, (double)field.GetValue(figures)!);
            }
        }

        foreach (var status in solution.StationStatus)
        {
            Append(hash, (int)status);
        }

        foreach (var iterations in solution.Iterations)
        {
            Append(hash, iterations);
        }

        Append(hash, (int)solution.Status);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The struct's fields in declaration order; <see cref="Type.GetFields()"/> promises no order, the metadata token carries it.</summary>
    private static FieldInfo[] Declared(Type type) => type.GetFields().OrderBy(field => field.MetadataToken).ToArray();

    private static void Append(IncrementalHash hash, double value) => hash.AppendData(BitConverter.GetBytes(BitConverter.DoubleToInt64Bits(value)));

    private static void Append(IncrementalHash hash, int value) => hash.AppendData(BitConverter.GetBytes(value));
}

/// <summary>
/// Bits level: the host solve of every rocket fixture gives the bits recorded in <c>Bits.approved.txt</c>. A tripwire, not a
/// contract (BOOT.md): a decomposition, a renaming or a reordering of code moves no line, so a moved line is a numerical change
/// and must be named in the commit that moves it.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class BitSnapshotTests(CpuFixture fixture)
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>?> Approved = new(ReadApproved);

    private static int _actualWritten;

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Performance.Tests", "Bits.approved.txt");

    public static string ActualPath => RepositoryPaths.Resolve("tests", "Performance.Tests", "Bits.actual.txt");

    public static IEnumerable<object[]> Cases() => RocketHost.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_rocket_fixture_gives_the_recorded_bits(string name)
    {
        var c = RocketHost.Load(name);
        var path = PathOf(c);
        var bits = RocketBits.Hash(RocketHost.Solve(fixture, c));
        var approved = Approved.Value;
        if (approved is null)
        {
            WriteActual();
            Assert.Fail($"No approved bit snapshot existed, so the bits of every rocket fixture were written to {ActualPath}. " +
                        "Satisfy yourself that the numerics are the ones this node's criteria record, rename it to Bits.approved.txt and commit it.");
        }

        if (!approved.TryGetValue(path, out var recorded))
        {
            WriteActual();
            Assert.Fail($"{path} is not in Bits.approved.txt; the bits of every rocket fixture were written to {ActualPath}. " +
                        "A fixture and its line are approved in the same commit.");
        }

        if (bits != recorded)
        {
            WriteActual();
            Assert.Fail($"{path}: the host solve gives {bits}, Bits.approved.txt records {recorded}. " +
                        "A decomposition, a renaming or a reordering of code moves no line, so look for the numerical change first. " +
                        $"If it is intended, replace Bits.approved.txt with {ActualPath} in the same commit and name the change there.");
        }
    }

    /// <summary>The fixture's path from the repository root, with forward slashes, as the snapshot spells it.</summary>
    private static string PathOf(CeaCase c) => Path.GetRelativePath(RepositoryPaths.Root, c.Path).Replace('\\', '/');

    private static IReadOnlyDictionary<string, string>? ReadApproved()
    {
        if (!File.Exists(ApprovedPath))
        {
            return null;
        }

        var lines = File.ReadAllLines(ApprovedPath).Where(line => line.Length > 0);
        return lines.Select(line => line.Split(' ', 2)).ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
    }

    /// <summary>Writes the bits of every fixture, one line per fixture sorted by path; once per run, however many cases fail.</summary>
    private void WriteActual()
    {
        if (Interlocked.Exchange(ref _actualWritten, 1) != 0)
        {
            return;
        }

        var lines = new List<string>();
        foreach (var row in RocketHost.Cases())
        {
            var c = RocketHost.Load((string)row[0]);
            lines.Add(PathOf(c) + " " + RocketBits.Hash(RocketHost.Solve(fixture, c)));
        }

        lines.Sort(StringComparer.Ordinal);
        File.WriteAllText(ActualPath, string.Join('\n', lines) + "\n");
    }
}
