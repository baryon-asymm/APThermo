using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace APThermo.Protocol.Tests;

/// <summary>A private temporary Python script and captured live process identities, each bounded by a self-stop.</summary>
internal sealed class PythonProcessProbe : IAsyncDisposable
{
    internal const string ExpectedAsciiOutput = "stdout: plain\n\0stderr: plain\n";
    internal const string ExpectedOutput = "stdout: π\n\0stderr: λ\n";
    private static readonly TimeSpan ReadyBound = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReapBound = TimeSpan.FromSeconds(5);
    private readonly string _nonce = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    private readonly string _directory;
    private readonly List<Process> _owned = [];
    private int _parentIndex = -1;
    private int _childIndex = -1;

    public PythonProcessProbe()
    {
        _directory = Path.Combine(Path.GetTempPath(), "apthermo-python-" + _nonce);
        _ = Directory.CreateDirectory(_directory);
        Script = Path.Combine(_directory, "probe.txt");
        File.WriteAllText(Script, Source, new UTF8Encoding(false));
    }

    public string Script { get; }
    public int ParentId => _owned[_parentIndex].Id;
    public bool ChildIsRunning => _childIndex >= 0 && !_owned[_childIndex].HasExited;
    private string Marker => Path.Combine(_directory, "ready.json");
    private string Acknowledgement => Path.Combine(_directory, "ack");

    public string[] Arguments(string mode, int exitCode = 0) =>
        [mode, Marker, Acknowledgement, _nonce, exitCode.ToString(CultureInfo.InvariantCulture)];

    public async Task AcknowledgeAsync()
    {
        using var deadline = new CancellationTokenSource(ReadyBound);
        while (!File.Exists(Marker))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token).ConfigureAwait(true);
        }
        using var data = JsonDocument.Parse(await File.ReadAllTextAsync(Marker, deadline.Token).ConfigureAwait(true));
        Assert.Equal(_nonce, data.RootElement.GetProperty("nonce").GetString());
        var childId = data.RootElement.GetProperty("pid").GetInt32();
        var parentId = data.RootElement.GetProperty("parent").GetInt32();
        _parentIndex = Capture(parentId);
        _childIndex = childId == parentId ? _parentIndex : Capture(childId);
        await File.WriteAllTextAsync(Acknowledgement, _nonce, deadline.Token).ConfigureAwait(true);
    }

    public async Task WaitForParentExitAsync()
    {
        Assert.True(_parentIndex >= 0);
        await _owned[_parentIndex].WaitForExitAsync().WaitAsync(ReapBound).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var process in _owned)
        {
            try
            {
                await ReapAsync(process).ConfigureAwait(true);
            }
            finally
            {
                process.Dispose();
            }
        }
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "apthermo-python-" + _nonce));
        Assert.Equal(expected, Path.GetFullPath(_directory));
        Directory.Delete(_directory, recursive: true);
    }

    private int Capture(int pid)
    {
        Assert.True(pid > 0);
        var process = Process.GetProcessById(pid);
        _ = process.SafeHandle;
        Assert.False(process.HasExited);
        _ = process.StartTime;
        _owned.Add(process);
        return _owned.Count - 1;
    }

    private static async Task ReapAsync(Process owned)
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
                Assert.Equal(owned.StartTime, current.StartTime);
                current.Kill(entireProcessTree: true);
                await current.WaitForExitAsync().WaitAsync(ReapBound).ConfigureAwait(true);
            }
        }
        catch (InvalidOperationException) when (owned.HasExited)
        {
            // A self-stop or the helper's tree kill won the owned exit race.
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
            // Only this acquisition reports that the captured positive PID no longer exists.
            return null;
        }
    }

    private const string Source = """
import json, os, subprocess, sys, time
from pathlib import Path
mode, marker, ack, nonce, exit_code = sys.argv[1:]
def ready(pid, parent):
    destination = Path(marker)
    temporary = destination.with_suffix('.tmp')
    temporary.write_text(json.dumps(dict(pid=pid, parent=parent, nonce=nonce)), encoding='utf-8')
    temporary.replace(destination)
def wait_ack():
    until = time.monotonic() + 12
    while not Path(ack).exists() and time.monotonic() < until:
        time.sleep(0.01)
if mode == 'child':
    ready(os.getpid(), os.getppid())
    time.sleep(12)
    sys.exit(0)
if mode == 'ascii':
    sys.stdout.buffer.write(b'stdout: plain\n\0')
    sys.stderr.buffer.write(b'stderr: plain\n')
    sys.exit(int(exit_code))
sys.stdout.buffer.write('stdout: π\n\0'.encode('utf-8'))
sys.stdout.buffer.flush()
sys.stderr.buffer.write('stderr: λ\n'.encode('utf-8'))
sys.stderr.buffer.flush()
if mode == 'normal':
    sys.exit(int(exit_code))
if mode == 'hang':
    ready(os.getpid(), os.getpid())
    wait_ack()
    time.sleep(12)
else:
    subprocess.Popen([sys.executable, '-X', 'utf8', __file__, 'child', marker, ack, nonce, '0'],
                     stdout=sys.stdout if mode != 'stderr' else subprocess.DEVNULL,
                     stderr=sys.stderr if mode != 'stdout' else subprocess.DEVNULL)
    wait_ack()
""";
}
