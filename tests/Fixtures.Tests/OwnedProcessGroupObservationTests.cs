using System.Reflection;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Linux session observation distinguishes disappearing processes from real observation errors.</summary>
public sealed class OwnedProcessGroupObservationTests
{
    private const int Session = 4242;
    private static readonly string[] Directories = ["/proc/101", "/proc/102", "/proc/sys"];
    private static readonly string[] ExpectedReads = ["101", "102"];
    private static readonly Func<int, IEnumerable<string>, Func<string, string>, bool> Observe = CreateObserver();

    /// <summary>An ESRCH stat read is skipped and the next live owned member is still found.</summary>
    [Fact]
    public void AnExitedProcessDoesNotHideALaterLiveSessionMember()
    {
        var reads = new List<string>();
        var alive = Observe(Session, Directories, path =>
        {
            var pid = PidOf(path);
            reads.Add(pid);
            return pid == "101" ? throw new IOException("gone", 3) : Stat(pid, "S", Session);
        });
        Assert.True(alive);
        Assert.Equal(ExpectedReads, reads);
    }

    /// <summary>Known missing paths are also skipped, but foreign sessions do not count as live.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GoneThenForeignSessionIsNotLive(int missing)
    {
        var alive = Observe(Session, Directories, path => PidOf(path) == "101"
            ? throw Missing(missing)
            : Stat("102", "S", Session + 1));
        Assert.False(alive);
    }

    /// <summary>Terminal kernel states are not live even when their recorded session matches.</summary>
    [Theory]
    [InlineData("Z")]
    [InlineData("X")]
    [InlineData("x")]
    public void TerminalStatesAreNotLive(string state) =>
        Assert.False(Observe(Session, Directories, path => Stat(PidOf(path), state, Session)));

    /// <summary>Permission and unrelated read failures retain their original exception.</summary>
    [Fact]
    public void OtherReadFailuresPropagate()
    {
        var failures = new Exception[] { new IOException("denied", 5), new UnauthorizedAccessException("denied") };
        foreach (var expected in failures)
        {
            var observed = Record.Exception(() => Observe(Session, Directories, _ => throw expected));
            Assert.Same(expected, observed);
        }
    }

    /// <summary>Even ESRCH while enumerating directories is not a per-process read and cannot be suppressed.</summary>
    [Fact]
    public void EnumerationFailurePropagates()
    {
        var expected = new IOException("enumeration", 3);
        var observed = Record.Exception(() => Observe(Session, FailingEnumeration(expected), _ => Stat("101", "S", Session + 1)));
        Assert.Same(expected, observed);
    }

    /// <summary>Malformed proc stat remains an observation error, not evidence of an empty session.</summary>
    [Fact]
    public void MalformedStatPropagates() =>
        Assert.Throws<InvalidDataException>(() => Observe(Session, Directories, _ => "101 (test) S 1"));

    private static Func<int, IEnumerable<string>, Func<string, string>, bool> CreateObserver()
    {
        var owner = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessGroup", throwOnError: true)!;
        var method = owner.GetMethod("HasLiveMembers", BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(int), typeof(IEnumerable<string>), typeof(Func<string, string>)], null)
            ?? throw new InvalidOperationException("owned session observation seam is absent");
        return method.CreateDelegate<Func<int, IEnumerable<string>, Func<string, string>, bool>>();
    }

    private static string PidOf(string path) => Path.GetFileName(Path.GetDirectoryName(path))
        ?? throw new InvalidOperationException("stat path has no process directory");

    private static string Stat(string pid, string state, int session) => $"{pid} (probe) {state} 1 11 {session}";

    private static IOException Missing(int kind) => kind switch
    {
        0 => new IOException("gone", 3),
        1 => new FileNotFoundException("gone"),
        _ => new DirectoryNotFoundException("gone"),
    };

    private static IEnumerable<string> FailingEnumeration(Exception error)
    {
        yield return "/proc/101";
        throw error;
    }
}
