using System.Text;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>
/// The warm solvers of <c>Program.RunCached</c> (`src/Cli` API.md, "Entry point (tree contract)"): a solver is kept
/// per database content and accelerator, so a second invocation reuses it; a warm invocation gives the bytes a fresh
/// one gives; the cache disposes what it created. The in-process facts of this node run on one such cache; the
/// process facts of the end-to-end set prove the cold path against the same approved bits.
/// </summary>
[Collection("cli")]
public sealed class SolverCacheTests
{
    private static string[] Rocket(string databaseDirectory) =>
        ["rocket", CliFixture.Document("rocket-lox-lh2.json"), "--database", databaseDirectory, "--accelerator", "cpu"];

    private static Run Cached(SolverCache cache, string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.RunCached(args, output, error, cache);
        return new Run(code, output.ToString(), error.ToString());
    }

    /// <summary>A copy of the committed database whose <c>thermo.inp</c> differs by one trailing comment line: the same species, another content hash.</summary>
    private static string DatabaseWithAnotherContentHash()
    {
        var directory = Directory.CreateDirectory(CliFixture.Shared.TempFile("another-content-db")).FullName;
        foreach (var file in (string[])[DatabaseFiles.ThermoFile, DatabaseFiles.TransFile])
        {
            File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, file), Path.Combine(directory, file), overwrite: true);
        }

        File.AppendAllText(Path.Combine(directory, DatabaseFiles.ThermoFile), "! a trailing comment, no species\n");
        return directory;
    }

    /// <summary>Invocations on one database content and accelerator share one solver: the count of solvers created stays one.</summary>
    [Fact]
    public void InvocationsOnTheSameDatabaseContentShareOneSolver()
    {
        using var cache = new SolverCache();
        Assert.Equal(0, cache.Count);
        var args = Rocket(CliFixture.Shared.DatabasePath);
        Assert.Equal(0, Cached(cache, args).Code);
        Assert.Equal(1, cache.Count);
        Assert.Equal(0, Cached(cache, args).Code);
        Assert.Equal(0, Cached(cache, [.. args, "--format", "csv"]).Code);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>A database of another content gets its own solver, and goes on sharing it.</summary>
    [Fact]
    public void ADifferentDatabaseContentGetsItsOwnSolver()
    {
        using var cache = new SolverCache();
        var committed = Rocket(CliFixture.Shared.DatabasePath);
        var another = Rocket(DatabaseWithAnotherContentHash());
        Assert.Equal(0, Cached(cache, committed).Code);
        Assert.Equal(0, Cached(cache, another).Code);
        Assert.Equal(2, cache.Count);
        Assert.Equal(0, Cached(cache, another).Code);
        Assert.Equal(0, Cached(cache, committed).Code);
        Assert.Equal(2, cache.Count);
    }

    /// <summary>A warm invocation, the second on its solver, delivers the bytes of a fresh invocation, the <c>run</c> section aside.</summary>
    [Fact]
    public void AWarmInvocationGivesTheBytesOfAFreshOne()
    {
        var args = Rocket(CliFixture.Shared.DatabasePath);
        using var fresh = new StringWriter();
        Assert.Equal(0, Program.Run(args, fresh, TextWriter.Null));

        using var cache = new SolverCache();
        _ = Cached(cache, args);
        var warm = Cached(cache, args);
        Assert.Equal(0, warm.Code);
        Assert.Equal(
            RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(fresh.ToString()), "the fresh invocation"),
            RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(warm.Output), "the warm invocation"));
    }

    /// <summary>Disposing the cache disposes every solver it created.</summary>
    [Fact]
    public void DisposingTheCacheReleasesItsSolvers()
    {
        var cache = new SolverCache();
        Assert.Equal(0, Cached(cache, Rocket(CliFixture.Shared.DatabasePath)).Code);
        Assert.Equal(1, cache.Count);
        cache.Dispose();
        Assert.Equal(0, cache.Count);
    }
}
