#!/usr/bin/env python3
"""Run an exhaustive hosted test-project lane with fresh, source-bound evidence."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PureWindowsPath
import re
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
LANES = ("core", "cli", "docs")
CLI = "tests/Cli.Tests/APThermo.Cli.Tests.csproj"
DOCS = "tests/Docs.Tests/APThermo.Docs.Tests.csproj"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def timestamp():
    return {"utc": datetime.now(timezone.utc).isoformat(), "monotonic_seconds": time.monotonic()}


def persist(results, attempt, manifest):
    for directory in (results, attempt):
        temporary = directory / "manifest.json.tmp"
        temporary.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        temporary.replace(directory / "manifest.json")


def source_identity(root, run):
    identity = {}
    for key, arguments in (("commit", ["rev-parse", "HEAD"]),
                           ("tracked_changes", ["status", "--porcelain", "--untracked-files=no"])):
        command = ["git", *arguments]
        result = run(command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
        if result.returncode != 0:
            raise RuntimeError("source identity command failed: " + repr(command))
        identity[key] = result.stdout.decode("utf-8", errors="replace").strip()
    if not re.fullmatch(r"[0-9a-fA-F]{40}|[0-9a-fA-F]{64}", identity["commit"]):
        raise RuntimeError("git returned an invalid source identity")
    identity["runner_sha256"] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    identity["github"] = {key: os.environ.get(key) for key in
                          ("GITHUB_SHA", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT", "GITHUB_WORKFLOW", "GITHUB_JOB")}
    return identity


def discover_projects(root, output):
    projects = []
    identities = set()
    for line in output.splitlines():
        candidate = line.strip().replace("\\", "/")
        if not candidate.endswith(".csproj"):
            continue
        if PureWindowsPath(candidate).drive or Path(candidate).is_absolute():
            raise RuntimeError("solution project must be repository-relative: " + candidate)
        path = (root / candidate).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise RuntimeError("solution project is outside the repository or missing: " + candidate)
        relative = path.relative_to(root).as_posix()
        key = relative.casefold()
        if key in identities:
            raise RuntimeError("duplicate solution project: " + relative)
        identities.add(key)
        projects.append(relative)
    tests = sorted(project for project in projects if Path(project).name.endswith(".Tests.csproj"))
    if not tests or CLI not in tests or DOCS not in tests:
        raise RuntimeError("test discovery is empty or lacks the designated CLI/Docs project")
    return sorted(projects), tests


def assignments(projects):
    lanes = {"core": [project for project in projects if project not in (CLI, DOCS)],
             "cli": [CLI], "docs": [DOCS]}
    verify_assignments(projects, lanes)
    return lanes


def verify_assignments(projects, lanes):
    if set(lanes) != set(LANES) or any(not lanes[lane] for lane in LANES):
        raise RuntimeError("all hosted lanes must be present and nonempty")
    combined = [project for lane in LANES for project in lanes[lane]]
    if len(combined) != len(set(combined)) or sorted(combined) != sorted(projects):
        raise RuntimeError("hosted lanes must be an exact disjoint partition")
    if lanes["cli"] != [CLI] or lanes["docs"] != [DOCS]:
        raise RuntimeError("CLI and Docs must own their designated projects")


def test_command(root, project, directory):
    return ["dotnet", "test", str(root / project), "--configuration", "Release", "--no-build", "-m:1",
            "--filter", "Category!=LongRunning", "--logger", "trx;LogFileName=tests.trx",
            "--results-directory", str(directory)]


def verify_report(path, record=None):
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError("missing nonempty fresh TRX: " + str(path))
    document = ET.parse(path)
    results = document.findall(".//t:UnitTestResult", NS)
    identities = [result.attrib.get("executionId") for result in results]
    if not results or None in identities or len(identities) != len(set(identities)):
        raise RuntimeError("TRX has empty or duplicate execution results")
    counters = document.findall(".//t:ResultSummary/t:Counters", NS)
    if len(counters) != 1:
        raise RuntimeError("TRX must have exactly one result summary counter set")
    counts = {key: int(value) for key, value in counters[0].attrib.items()}
    if record is not None:
        record["counters"] = counts
    if any(counts.get(key) != len(results) for key in ("total", "executed", "passed")):
        raise RuntimeError("TRX counters are empty or inconsistent with results")
    if counts.get("failed") != 0 or any(value != 0 for key, value in counts.items()
                                       if key not in ("total", "executed", "passed")):
        raise RuntimeError("TRX contains failed, skipped or incomplete tests")
    if any(result.attrib.get("outcome") != "Passed" for result in results):
        raise RuntimeError("TRX contains failed or skipped test results")
    summary = document.find(".//t:ResultSummary", NS)
    if summary.attrib.get("outcome") != "Completed":
        raise RuntimeError("TRX summary is not completed")
    return counts


def replay(path):
    with path.open("rb") as stream:
        while chunk := stream.read(65536):
            sys.stdout.buffer.write(chunk)
    sys.stdout.buffer.flush()


def execute_project(root, directory, record, save, run):
    record.update(status="started", started=timestamp())
    save()
    log = directory / "execution.log"
    try:
        directory.mkdir(parents=True)
        print("Running " + repr(record["command"]), flush=True)
        with log.open("wb") as output:
            result = run(record["command"], cwd=root, env=dict(os.environ, APTHERMO_NO_CUDA="1"),
                         stdout=output, stderr=subprocess.STDOUT)
        record["exit_code"] = result.returncode
        # Parse even a failed run, preserving the independent command/report outcomes.
        try:
            record["counters"] = verify_report(directory / "tests.trx", record)
        except (OSError, ValueError, RuntimeError, ET.ParseError) as error:
            record["report_error"] = str(error)
        if result.returncode != 0 or "report_error" in record:
            raise RuntimeError("test command or TRX verification failed")
        record["status"] = "passed"
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        record.update(status="failed", error=str(error))
    finally:
        record["finished"] = timestamp()
        record["elapsed_seconds"] = record["finished"]["monotonic_seconds"] - record["started"]["monotonic_seconds"]
        save()
        if log.exists():
            replay(log)


def execute(root, results, lane, run=subprocess.run):
    root, results = root.resolve(), results.resolve()
    results.mkdir(parents=True, exist_ok=True)
    attempt = results / ("attempt-" + uuid.uuid4().hex)
    attempt.mkdir()
    manifest = {"schema": 1, "lane": lane, "root": str(root), "attempt": str(attempt),
                "status": "started", "started": timestamp(), "executions": []}
    save = lambda: persist(results, attempt, manifest)
    save()
    try:
        if lane not in LANES:
            raise RuntimeError("unknown hosted lane")
        manifest["source"] = source_identity(root, run)
        command = ["dotnet", "sln", "APThermo.sln", "list"]
        discovery = {"command": command, "status": "started", "started": timestamp()}
        manifest["discovery"] = discovery
        save()
        with (attempt / "discovery.log").open("wb") as output:
            result = run(command, cwd=root, stdout=output, stderr=subprocess.STDOUT, timeout=120)
        discovery.update(exit_code=result.returncode, finished=timestamp())
        discovery["elapsed_seconds"] = discovery["finished"]["monotonic_seconds"] - discovery["started"]["monotonic_seconds"]
        if result.returncode != 0:
            raise RuntimeError("dotnet solution discovery failed")
        text = (attempt / "discovery.log").read_text(encoding="utf-8-sig", errors="replace")
        solution, projects = discover_projects(root, text)
        lanes = assignments(projects)
        discovery["status"] = "passed"
        manifest.update(solution_projects=solution, discovered_projects=projects, assignments=lanes, selected_projects=lanes[lane])
        for index, project in enumerate(lanes[lane]):
            directory = attempt / (str(index + 1).zfill(2) + "-" + Path(project).stem)
            manifest["executions"].append({"project": project, "results_directory": str(directory),
                                            "command": test_command(root, project, directory), "status": "not-run"})
        save()
        for record in manifest["executions"]:
            execute_project(root, Path(record["results_directory"]), record, save, run)
        manifest["status"] = "passed" if all(record["status"] == "passed" for record in manifest["executions"]) else "failed"
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        manifest.update(status="failed", error=str(error))
        if manifest.get("discovery", {}).get("status") == "started":
            discovery = manifest["discovery"]
            discovery.update(status="failed", error=str(error), finished=timestamp())
            discovery["elapsed_seconds"] = discovery["finished"]["monotonic_seconds"] - discovery["started"]["monotonic_seconds"]
    finally:
        manifest["finished"] = timestamp()
        manifest["elapsed_seconds"] = manifest["finished"]["monotonic_seconds"] - manifest["started"]["monotonic_seconds"]
        save()
    return 0 if manifest["status"] == "passed" else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lane", choices=LANES, required=True)
    parser.add_argument("--results-dir", type=Path, required=True)
    arguments = parser.parse_args()
    return execute(ROOT, arguments.results_dir, arguments.lane)


if __name__ == "__main__":
    sys.exit(main())
