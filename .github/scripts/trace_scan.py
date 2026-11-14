#!/usr/bin/env python3
"""Run the documented CPU scans with nonempty discovery and execution evidence."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OWNER = "APThermo.Equilibrium.Tests.TraceGasScanTests"
PROJECT = "tests/Equilibrium.Tests/APThermo.Equilibrium.Tests.csproj"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def documented_methods(root):
    text = (root / "tests/Equilibrium.Tests/API.md").read_text(encoding="utf-8-sig")
    section = re.search(r"^## Long-running verification entry points ✅\s*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    if section is None:
        raise RuntimeError("missing implemented long-running verification contract")
    blocks = []
    for block in re.finditer(r"^```csharp[ \t]*\n(.*?)^```[ \t]*$", section[1], re.M | re.S):
        prefix = re.sub(r"```.*?```", "", section[1][:block.start()], flags=re.S)
        marks = re.findall("[✅⏳]", prefix)
        if not marks or marks[-1] == "✅":
            blocks.append(block[1])
    owners = [re.search(r"class\s+TraceGasScanTests\s*\{([^}]*)\}", block, re.S) for block in blocks]
    owners = [owner for owner in owners if owner]
    if len(owners) != 1:
        raise RuntimeError("expected one checked TraceGasScanTests contract")
    names = re.findall(r"public\s+void\s+(\w+)\s*\(\s*\)\s*;", owners[0][1])
    if not names or len(set(names)) != len(names):
        raise RuntimeError("scan contract is empty or duplicated")
    return sorted(OWNER + "." + name for name in names)


def verify_report(path, expected):
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError("missing nonempty trace-gas.trx")
    document = ET.parse(path)
    definitions = {}
    for test in document.findall(".//t:UnitTest", NS):
        method = test.find("t:TestMethod", NS)
        if method is not None:
            definitions[test.attrib["id"]] = method.attrib["className"] + "." + method.attrib["name"]
    results = document.findall(".//t:UnitTestResult", NS)
    names = [definitions.get(result.attrib.get("testId")) for result in results]
    if sorted(name or "" for name in names) != expected or len(set(names)) != len(names):
        raise RuntimeError("TRX executed methods differ from the exact scan contract")
    if any(result.attrib.get("outcome") != "Passed" for result in results):
        raise RuntimeError("a scan failed or was skipped")
    counters = document.find(".//t:Counters", NS)
    if counters is None or any(int(counters.attrib.get(key, -1)) != len(expected)
                               for key in ("total", "executed", "passed")):
        raise RuntimeError("TRX has inconsistent or empty scan counters")
    if int(counters.attrib.get("failed", -1)) != 0:
        raise RuntimeError("TRX reports failed scans")


def execute(root, artifacts, configuration="Release", discover_only=False, run=subprocess.run):
    artifacts.mkdir(parents=True, exist_ok=True)
    expected = documented_methods(root)
    selection = "Category=LongRunning&(" + "|".join("FullyQualifiedName=" + name for name in expected) + ")"
    command = ["dotnet", "test", str(root / PROJECT), "-m:1", "--configuration", configuration,
               "--no-build", "--filter", selection]
    environment = dict(os.environ, APTHERMO_NO_CUDA="1")
    with (artifacts / "discovery.log").open("wb") as log:
        discovery = run(command + ["--list-tests"], cwd=root, env=environment,
                        stdout=log, stderr=subprocess.STDOUT, timeout=120)
    discovered_text = (artifacts / "discovery.log").read_text(encoding="utf-8-sig", errors="replace")
    found = [line.strip() for line in discovered_text.splitlines() if line.strip().startswith(OWNER + ".")]
    if discovery.returncode != 0 or sorted(found) != expected or len(set(found)) != len(found):
        raise RuntimeError("discovery failed or differs from the nonempty scan contract")
    matrix = [{"test": name, "category": "LongRunning", "job": "trace-scan", "accelerator": "CPU",
               "execution": "not-run"} for name in found]
    (artifacts / "matrix.json").write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")
    if discover_only:
        return
    for entry in matrix:
        entry["execution"] = "started"
    (artifacts / "matrix.json").write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")
    previous_report = artifacts / "trace-gas.trx"
    if previous_report.exists() or previous_report.is_symlink():
        if not previous_report.is_file() and not previous_report.is_symlink():
            raise RuntimeError("existing trace-gas.trx is not a report file")
        previous_report.rename(artifacts / ("trace-gas.previous-" + uuid.uuid4().hex + ".trx"))
    with (artifacts / "execution.log").open("wb") as log:
        result = run(command + ["--logger", "trx;LogFileName=trace-gas.trx", "--results-directory", str(artifacts)],
                     cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=1200)
    if result.returncode != 0:
        raise RuntimeError("dotnet scan execution failed; inspect execution.log and TRX")
    verify_report(artifacts / "trace-gas.trx", expected)
    for entry in matrix:
        entry["execution"] = "passed"
    (artifacts / "matrix.json").write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--results-dir", type=Path, default=ROOT / "artifacts/trace-gas")
    parser.add_argument("--discover-only", action="store_true")
    args = parser.parse_args()
    artifacts = args.results_dir.resolve()
    try:
        execute(ROOT, artifacts, args.configuration, args.discover_only)
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired) as error:
        artifacts.mkdir(parents=True, exist_ok=True)
        (artifacts / "runner-error.txt").write_text(str(error) + "\n", encoding="utf-8")
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
