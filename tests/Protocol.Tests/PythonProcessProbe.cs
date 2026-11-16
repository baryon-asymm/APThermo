using System.Diagnostics;
using System.Globalization;
using System.Text;
using APThermo.Harness;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>Owns one real fixture watchdog and a nonce-validated pre-exit readiness protocol.</summary>
internal sealed class PythonProcessProbe : IAsyncDisposable
{
    internal static readonly TimeSpan LogicalDeadline = TimeSpan.FromSeconds(3);
    internal const string ExpectedAsciiOutput = PythonProbeScript.ExpectedAsciiOutput;
    internal const string ExpectedOutput = PythonProbeScript.ExpectedOutput;
    private readonly string _nonce = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    private readonly string _directory;
    private readonly List<Process> _owned = [];
    private readonly Dictionary<int, DateTime> _starts = [];
    private readonly List<string> _history = [];
    private readonly PythonFixtureBudget _budget = new();
    private Task<PythonRun>? _call;
    private PythonProbeFrame? _ready;
    private string _mode = string.Empty;
    private int _parentIndex = -1;
    private int _childIndex = -1;
    private bool _systemClock;
    private TimeSpan _timeout;

    public PythonProcessProbe()
    {
        _directory = Path.Combine(Path.GetTempPath(), "apthermo-python-" + _nonce);
        _ = Directory.CreateDirectory(_directory);
        Script = Path.Combine(_directory, "probe.txt");
        File.WriteAllText(Script, PythonProbeScript.Source, new UTF8Encoding(false));
        Record("fixture-created");
    }

    public PythonDeadlineClock Clock { get; } = new();
    public PythonDeadlineClock PreparationClock { get; } = new();
    public string Script { get; }
    public int ParentId => _owned[_parentIndex].Id;
    public bool ChildIsRunning => _childIndex >= 0 && !_owned[_childIndex].HasExited;
    public bool CallCompleted => _call?.IsCompleted ?? false;
    public string Diagnostics => string.Join(Environment.NewLine, _history);
    public TimeSpan Remaining => _budget.Remaining;
    private string Marker => Path.Combine(_directory, "ready.json");
    private string Acknowledgement => Path.Combine(_directory, "ack.json");
    private string Release => Path.Combine(_directory, "release.json");

    public void Start(string mode, TimeSpan timeout, int exitCode = 0, bool systemClock = false,
        Func<ProcessStartInfo, CancellationToken, Action<int>, Task<DotnetProcessResult>>? controlledRun = null)
    {
        _budget.Require("dispatch");
        _mode = mode;
        _systemClock = systemClock;
        _timeout = timeout;
        var arguments = new[] { "parent", mode, Marker, Acknowledgement, Release, _nonce,
            exitCode.ToString(CultureInfo.InvariantCulture), "0" };
        _call = Task.Run(() => systemClock
            ? PythonProcess.RunAsync(Script, arguments, timeout)
            : PythonProcess.RunAsync(Script, arguments, timeout, Clock, controlledRun), _budget.Token);
        Record("helper-dispatched");
    }

    public async Task AcknowledgeAsync(Action? prepare = null)
    {
        _budget.Require("readiness");
        var stage = _mode == "hang" ? "parent-ready" : "child-ready";
        var ready = await ReadFrameAsync(Marker, stage).ConfigureAwait(true);
        prepare?.Invoke();
        _budget.Require("identity-acquisition");
        _ready = ready;
        _parentIndex = Capture(ready.Parent);
        _childIndex = ready.Child == ready.Parent ? _parentIndex : Capture(ready.Child);
        Record("live-identities-held");
        await WriteFrameAsync(Acknowledgement, ready with { Stage = "identities-held" }, _budget.Token).ConfigureAwait(true);
        var accepted = await ReadFrameAsync(Acknowledgement + ".accepted", "acknowledged").ConfigureAwait(true);
        if (accepted.Parent != ready.Parent || accepted.Child != ready.Child)
        {
            throw new InvalidOperationException("fixture acknowledged identities changed");
        }
        Record("nonce-ack-validated");
    }

    public async Task WaitForParentExitAsync(bool requireSuccess = true)
    {
        _budget.Require("parent-exit");
        await _budget.WaitAsync(_owned[_parentIndex].WaitForExitAsync(), "parent-exit").ConfigureAwait(true);
        Record("parent-exited");
        if (requireSuccess && _owned[_parentIndex].ExitCode != 0)
        {
            throw new InvalidOperationException($"fixture parent exited with {_owned[_parentIndex].ExitCode}");
        }
    }

    public void RequireLiveChild()
    {
        Record("child-liveness-check");
        _budget.Require("child-liveness");
        if (!ChildIsRunning)
        {
            throw new InvalidOperationException("fixture owned pipe identity is not live");
        }
    }

    public void AdvanceDeadline()
    {
        _budget.Require("deadline-advance");
        Record("before-deadline-advance");
        Clock.Advance(LogicalDeadline);
        Record("deadline-advanced");
    }

    public Task<PythonRun> CompletionAsync()
    {
        Assert.NotNull(_call);
        return _budget.WaitAsync(_call, "helper-completion");
    }

    public async Task ReleaseAsync()
    {
        Assert.NotNull(_ready);
        await WriteFrameAsync(Release, _ready with { Stage = "release" }, _budget.Token).ConfigureAwait(true);
        Record("controller-released");
    }

    public void Record(string stage)
    {
        var due = string.Join(",", Clock.DueTimes.Select(value => value.TotalSeconds.ToString(CultureInfo.InvariantCulture)));
        var identities = string.Join(",", _starts.Select(pair => $"{pair.Key}:{pair.Value.ToUniversalTime().Ticks}"));
        _history.Add($"{stage}: monotonic={Stopwatch.GetTimestamp()}; remaining={_budget.Remaining}; logical={Clock.GetTimestamp()}; preparation={PreparationClock.GetTimestamp()}; dueSeconds=[{due}]; fired={Clock.Fired}; identities=[{identities}]; nonce={_nonce}; mode={_mode}");
    }

    public async ValueTask DisposeAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            if (_ready is not null)
            {
                await WriteFrameAsync(Release, _ready with { Stage = "release" }, cleanup.Token).ConfigureAwait(true);
            }
            if (_call is { IsCompleted: false } && !_systemClock)
            {
                Clock.Advance(_timeout);
            }
            var reap = _owned.Select(process => ReapAsync(process, cleanup.Token));
            var tasks = _call is null ? reap : reap.Append(ObserveCallAsync(_call));
            await Task.WhenAll(tasks).WaitAsync(cleanup.Token).ConfigureAwait(true);
        }
        finally
        {
            DisposeResources();
        }
    }

    private int Capture(int pid)
    {
        _budget.Require("capture");
        var process = Process.GetProcessById(pid);
        _owned.Add(process);
        var index = _owned.Count - 1;
        _ = process.SafeHandle;
        if (process.HasExited)
        {
            throw new InvalidOperationException($"fixture identity {pid} exited during capture");
        }
        _starts.Add(pid, process.StartTime);
        return index;
    }

    private async Task<PythonProbeFrame> ReadFrameAsync(string path, string stage)
    {
        while (!File.Exists(path))
        {
            _budget.Require(stage);
            await _budget.WaitAsync(Task.Delay(TimeSpan.FromMilliseconds(10), _budget.Token), stage).ConfigureAwait(true);
        }
        var text = await _budget.WaitAsync(File.ReadAllTextAsync(path, _budget.Token), stage).ConfigureAwait(true);
        var frame = PythonProbeFrame.Parse(text);
        frame.Validate(_nonce, _mode, stage);
        Record(stage);
        return frame;
    }

    private static async Task WriteFrameAsync(string path, PythonProbeFrame frame, CancellationToken token)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, frame.Serialize(), token).ConfigureAwait(true);
        File.Move(temporary, path, overwrite: true);
    }

    private async Task ReapAsync(Process owned, CancellationToken token)
    {
        try
        {
            if (!owned.HasExited)
            {
                using var current = FindCurrent(owned.Id);
                if (current is null)
                {
                    return;
                }
                if (!_starts.TryGetValue(owned.Id, out var start) || current.StartTime != start)
                {
                    throw new InvalidOperationException($"fixture cannot verify identity {owned.Id} for teardown");
                }
                await Task.Run(() => current.Kill(entireProcessTree: true), token).ConfigureAwait(true);
                await current.WaitForExitAsync(token).ConfigureAwait(true);
            }
        }
        catch (InvalidOperationException) when (owned.HasExited)
        {
            // The controller release or helper cleanup won this owned exit race.
        }
    }

    private static Process? FindCurrent(int pid)
    {
        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task ObserveCallAsync(Task call)
    {
        try
        {
            await call.ConfigureAwait(true);
        }
        catch (XunitException)
        {
            // A helper deadline is the expected observed outcome of interrupted probes.
        }
    }

    private void DisposeResources()
    {
        foreach (var process in _owned)
        {
            process.Dispose();
        }
        Clock.Dispose();
        PreparationClock.Dispose();
        _budget.Dispose();
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "apthermo-python-" + _nonce));
        Assert.Equal(expected, Path.GetFullPath(_directory));
        Directory.Delete(_directory, recursive: true);
    }
}
