using System.Security.Cryptography;
using System.Text;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>
/// Bits: the host solve of every tp, hp and sp fixture case gives the recorded bits. A tripwire, not a contract (BOOT.md):
/// it cannot say that a number is right — the fixtures node's tolerance table says that — only that no number moved without
/// the commit saying so. The same role <c>PublicSurface.approved.txt</c> plays for the contract, and the same shape:
/// one approved file, an actual file written beside it on a difference, and instructions in the failure.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class BitSnapshotTests(CpuFixture fixture)
{
    /// <summary>The problem kinds whose fixture directories the snapshot covers.</summary>
    private static readonly string[] Kinds = ["tp", "hp", "sp"];

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "Bits.approved.txt");

    public static string ActualPath => RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "Bits.actual.txt");

    [Fact]
    public void Every_fixture_case_gives_the_recorded_bits()
    {
        var actual = Snapshot();
        Assert.NotEmpty(actual);
        if (!File.Exists(ApprovedPath))
        {
            File.WriteAllText(ActualPath, Render(actual));
            Assert.Fail($"No approved bit snapshot existed. What the tree produces now was written to {ActualPath}. " +
                        "Satisfy yourself that these results are the ones the fixtures node's tolerance table accepts, " +
                        $"rename the file to {ApprovedPath} and commit it in the same commit as the change that made it.");
        }

        var approved = Read(ApprovedPath);
        var differences = Differences(approved, actual);
        if (differences.Count == 0)
        {
            File.Delete(ActualPath);
            return;
        }

        File.WriteAllText(ActualPath, Render(actual));
        Assert.Fail($"{differences.Count} fixture case(s) no longer give the recorded bits:\n" + string.Join("\n", differences.Take(20)) +
                    (differences.Count > 20 ? $"\n… and {differences.Count - 20} more." : string.Empty) +
                    $"\nWhat the tree produces now was written to {ActualPath}. A decomposition, a renaming or a reordering of code " +
                    "moves no line here; if a numerical change was intended, name it in the commit message and replace " +
                    $"{ApprovedPath} with the actual file in that same commit.");
    }

    /// <summary>The relative path of every enumerated tp, hp and sp fixture with the hash of its solution's raw bits, ordered by path.</summary>
    private SortedDictionary<string, string> Snapshot()
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kind in Kinds)
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                snapshot.Add(kind + "/" + name + ".json", Hash(HostSolver.Solve(fixture, HostSolver.Load(kind, name))));
            }
        }

        return snapshot;
    }

    /// <summary>
    /// SHA-256, lower-case hex, over the raw bits of the whole result in one order: every mole, every multiplier, every field
    /// of <see cref="MixtureState"/> in declaration order (by reflection, so that a field added there cannot be forgotten),
    /// the status and the iteration count.
    /// </summary>
    private static string Hash(HostSolution solution)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (var moles in solution.Moles)
        {
            writer.Write(BitConverter.DoubleToInt64Bits(moles));
        }

        foreach (var multiplier in solution.Multipliers)
        {
            writer.Write(BitConverter.DoubleToInt64Bits(multiplier));
        }

        foreach (var field in typeof(MixtureState).GetFields())
        {
            writer.Write(BitConverter.DoubleToInt64Bits((double)field.GetValue(solution.State)!));
        }

        writer.Write((int)solution.Status);
        writer.Write(solution.Iterations);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    /// <summary>Every case whose hash moved and every case the approved file does not know, as messages.</summary>
    private static List<string> Differences(IReadOnlyDictionary<string, string> approved, SortedDictionary<string, string> actual)
    {
        var differences = new List<string>();
        foreach (var (path, hash) in actual)
        {
            if (!approved.TryGetValue(path, out var recorded))
            {
                differences.Add($"{path}: not in the approved snapshot (a new fixture case is approved like a changed one)");
            }
            else if (recorded != hash)
            {
                differences.Add($"{path}: approved {recorded}, now {hash}");
            }
        }

        foreach (var path in approved.Keys)
        {
            if (!actual.ContainsKey(path))
            {
                differences.Add($"{path}: in the approved snapshot but no longer a fixture case");
            }
        }

        return differences;
    }

    private static string Render(SortedDictionary<string, string> snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# The raw bits of every tp, hp and sp fixture solve on the CPU accelerator, as SHA-256 per case.");
        builder.AppendLine("# Generated by Equilibrium.Tests.BitSnapshotTests; a tripwire, not a contract: the contract is the node's API.md");
        builder.AppendLine("# and the fixtures node's tolerance table. Hashed per case, in this order: every mole, every multiplier,");
        builder.AppendLine("# every field of MixtureState in declaration order, the status, the iteration count.");
        foreach (var (path, hash) in snapshot)
        {
            builder.AppendLine(path + " " + hash);
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> Read(string path)
    {
        var recorded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var text = line.Trim();
            if (text.Length == 0 || text[0] == '#')
            {
                continue;
            }

            var space = text.IndexOf(' ');
            Assert.True(space > 0, $"{path}: '{line}' is not a case path and a hash");
            recorded.Add(text[..space], text[(space + 1)..]);
        }

        return recorded;
    }
}
