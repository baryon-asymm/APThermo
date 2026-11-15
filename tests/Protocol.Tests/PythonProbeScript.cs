namespace APThermo.Protocol.Tests;

/// <summary>The independent Python side of the owned ready/ack/release protocol.</summary>
internal static class PythonProbeScript
{
    internal const string ExpectedAsciiOutput = "stdout: plain\n\0stderr: plain\n";
    internal const string ExpectedOutput = "stdout: π\n\0stderr: λ\n";
    internal const string Source = """
import json, os, subprocess, sys, time
from pathlib import Path
role, mode, marker, ack, release, nonce, exit_code, parent_pid = sys.argv[1:]
until = time.monotonic() + 60
def publish(path, stage, parent, child):
    destination = Path(path)
    temporary = destination.with_suffix('.tmp')
    temporary.write_text(json.dumps(dict(nonce=nonce, mode=mode, stage=stage, parent=parent, child=child)), encoding='utf-8')
    temporary.replace(destination)
def wait_frame(path, stage, parent, child):
    expected = dict(nonce=nonce, mode=mode, stage=stage, parent=parent, child=child)
    while time.monotonic() < until:
        if Path(path).exists():
            if json.loads(Path(path).read_text(encoding='utf-8')) != expected:
                raise RuntimeError('fixture controller frame mismatch')
            return
        time.sleep(0.01)
    raise RuntimeError('fixture independent sixty-second watchdog')
if role == 'child':
    parent = int(parent_pid)
    publish(marker, 'child-ready', parent, os.getpid())
    wait_frame(release, 'release', parent, os.getpid())
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
    parent = child = os.getpid()
    publish(marker, 'parent-ready', parent, child)
else:
    parent = os.getpid()
    child = subprocess.Popen([sys.executable, '-X', 'utf8', __file__, 'child', mode, marker, ack, release, nonce, '0', str(parent)],
                             stdout=sys.stdout if mode != 'stderr' else subprocess.DEVNULL,
                             stderr=sys.stderr if mode != 'stdout' else subprocess.DEVNULL).pid
wait_frame(ack, 'identities-held', parent, child)
publish(ack + '.accepted', 'acknowledged', parent, child)
if mode == 'hang':
    wait_frame(release, 'release', parent, child)
""";
}
