"""Publish the tested commit while Git holds compare-and-swap locks on both tips."""
import os
from pathlib import Path
import signal
import subprocess
import sys


def git(*args, **options):
    return subprocess.run(["git", "-c", "core.longpaths=true", *args], check=True, **options)


def value(*args):
    return git(*args, stdout=subprocess.PIPE).stdout.rstrip(b"\r\n")


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def check_checkout(target):
    require(value("symbolic-ref", "HEAD") == os.fsencode(target), "target checkout switched")
    require(not value("--no-optional-locks", "status", "--porcelain", "-uno",
                      "--ignore-submodules=none"), "target checkout changed")
    for marker in ("MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "REBASE_HEAD",
                   "rebase-apply", "rebase-merge", "sequencer", "SQUASH_MSG"):
        path = Path(os.fsdecode(value("rev-parse", "--git-path", marker)))
        require(not path.exists(), f"target checkout has a pending operation ({marker})")


def deferred_interrupts():
    interrupted = []
    for sig in (signal.SIGINT, signal.SIGTERM):
        signal.signal(sig, lambda signum, frame: interrupted.append(signum))
    return interrupted


def rollback_checkout(target, target_id, candidate, tree):
    """Restore only our unchanged checkout; concurrent edits are never discarded."""
    if (value("rev-parse", target) == target_id.encode("ascii")
        and value("write-tree") == tree.encode("ascii")
        and subprocess.run(["git", "diff-files", "--quiet"]).returncode == 0):
        patch = git("diff", "--binary", "--full-index", "--no-renames", "--no-ext-diff",
                    "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/",
                    candidate, target_id, "--", stdout=subprocess.PIPE).stdout
        if patch:
            # Refuse to overwrite files created since checkout, including ignored ones.
            git("apply", "--index", "--whitespace=nowarn", "-", input=patch)
    else:
        print("GUARD: inspect the target checkout before retrying", file=sys.stderr)


def publish(source, source_id, target, target_id, candidate, tree):
    # prepare locks the refs and checks both expected OIDs in the same transaction.
    # Git's --squash checkout writes the index and files without moving HEAD. The
    # transaction publishes the already constructed two-parent commit, unchanged.
    # Defer an interrupt received during the short checkout until Git returns, so
    # the rollback can see whether the working files actually changed.
    interrupted = deferred_interrupts()
    check_checkout(target)
    with subprocess.Popen(["git", "update-ref", "-m", f"merge {source}", "--stdin"],
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE) as tx:
        def command(line):
            tx.stdin.write(os.fsencode(line) + b"\n")
            tx.stdin.flush()

        def expect(line):
            require(tx.stdout.readline().rstrip(b"\r\n") == line, "Git ref transaction failed")

        prepared = False
        checked_out = False
        squash_msg = Path(os.fsdecode(value("rev-parse", "--git-path", "SQUASH_MSG")))
        try:
            command("start")
            expect(b"start: ok")
            command(f"verify {source} {source_id}")
            command(f"update {target} {candidate} {target_id}")
            command("prepare")
            expect(b"prepare: ok")
            prepared = True
            check_checkout(target)
            require(not interrupted, "publication interrupted")
            if candidate != target_id:
                # This is a checkout operation. A post-merge hook would run a second,
                # untested action in the main working copy.
                git("-c", f"core.hooksPath={os.devnull}", "merge", "--squash", "--ff-only",
                    "--no-overwrite-ignore", "--no-autostash", "--no-stat", candidate)
                checked_out = True
            require(value("write-tree") == tree.encode("ascii"), "target index differs from tested tree")
            git("diff-files", "--quiet", "--ignore-submodules=none")
            require(value("symbolic-ref", "HEAD") == os.fsencode(target), "target checkout switched")
            require(not interrupted, "publication interrupted")
            command("commit")
            expect(b"commit: ok")
            prepared = False
        finally:
            if prepared and checked_out and value("symbolic-ref", "HEAD") == os.fsencode(target):
                rollback_checkout(target, target_id, candidate, tree)
            if checked_out:
                squash_msg.unlink(missing_ok=True)
            # EOF discards an uncommitted transaction and releases all ref locks.
            tx.stdin.close()
        require(tx.wait() == 0, "Git ref transaction failed")


if __name__ == "__main__":
    try:
        publish(*sys.argv[1:])
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        sys.exit(f"GUARD: {error}")
