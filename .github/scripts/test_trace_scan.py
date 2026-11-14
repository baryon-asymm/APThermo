#!/usr/bin/env python3
"""Non-degeneracy controls for CPU scan selection and evidence; no numerical scan runs here."""
import fnmatch
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

import trace_scan as scan


class TraceRunnerGuards(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.artifacts = self.root / "evidence"
        self.contract = self.root / "tests/Equilibrium.Tests/API.md"
        self.contract.parent.mkdir(parents=True)
        self.contract.write_text((scan.ROOT / "tests/Equilibrium.Tests/API.md").read_text(encoding="utf-8-sig"), encoding="utf-8")
        self.expected = scan.documented_methods(self.root)
        self.assertEqual(2, len(self.expected), "the current owning contract must expose both scan facts")
        self.calls = []

    def report(self, outcome="Passed", names=None, total=None):
        names = self.expected if names is None else names
        root = ET.Element("TestRun", xmlns=scan.NS["t"])
        definitions = ET.SubElement(root, "TestDefinitions")
        results = ET.SubElement(root, "Results")
        for index, name in enumerate(names):
            owner, method = name.rsplit(".", 1)
            test = ET.SubElement(definitions, "UnitTest", id=str(index))
            ET.SubElement(test, "TestMethod", className=owner, name=method)
            ET.SubElement(results, "UnitTestResult", testId=str(index), outcome=outcome)
        summary = ET.SubElement(root, "ResultSummary")
        count = str(len(names) if total is None else total)
        ET.SubElement(summary, "Counters", total=count, executed=count, passed=count, failed="0")
        ET.ElementTree(root).write(self.artifacts / "trace-gas.trx", encoding="utf-8", xml_declaration=True)

    def executor(self, discovered=None, discovery_exit=0, execution_exit=0, report=True, outcome="Passed", names=None, total=None):
        def run(command, **options):
            self.calls.append(command)
            self.assertEqual("1", options["env"]["APTHERMO_NO_CUDA"])
            self.assertIn("--no-build", command)
            selection = command[command.index("--filter") + 1]
            self.assertIn("Category=LongRunning", selection)
            for name in self.expected:
                self.assertIn("FullyQualifiedName=" + name, selection)
            if "--list-tests" in command:
                values = self.expected if discovered is None else discovered
                options["stdout"].write(("The following Tests are available:\n" + "\n".join("    " + name for name in values)).encode())
                return subprocess.CompletedProcess(command, discovery_exit)
            options["stdout"].write(b"controlled execution\n")
            if report:
                self.report(outcome, names, total)
            return subprocess.CompletedProcess(command, execution_exit)
        return run

    def test_exact_discovery_and_successful_report_publish_the_matrix(self):
        scan.execute(self.root, self.artifacts, run=self.executor())
        matrix = json.loads((self.artifacts / "matrix.json").read_text())
        self.assertEqual(self.expected, sorted(entry["test"] for entry in matrix))
        self.assertTrue(all(entry["execution"] == "passed" and entry["accelerator"] == "CPU" for entry in matrix))
        self.assertEqual(2, len(self.calls))
        self.assertTrue((self.artifacts / "execution.log").stat().st_size > 0)

    def test_empty_missing_extra_or_duplicate_discovery_refuses_before_execution(self):
        for found in [[], self.expected[:1], self.expected + ["unexpected"], self.expected + self.expected[:1]]:
            with self.subTest(found=found):
                # An unexpected fully-qualified scan must not disappear during prefix filtering.
                found = [scan.OWNER + ".Extra" if name == "unexpected" else name for name in found]
                with self.assertRaises(RuntimeError):
                    scan.execute(self.root, self.artifacts, run=self.executor(discovered=found))
                self.assertIn("--list-tests", self.calls[-1])

    def test_nonzero_discovery_refuses_even_with_correct_names(self):
        with self.assertRaises(RuntimeError):
            scan.execute(self.root, self.artifacts, run=self.executor(discovery_exit=1))

    def test_failed_missing_skipped_wrong_or_empty_report_refuses(self):
        for values in [{"execution_exit": 1}, {"report": False}, {"outcome": "NotExecuted"},
                       {"outcome": "Failed"}, {"names": []}, {"names": self.expected[:1]},
                       {"names": self.expected + self.expected[:1]}, {"total": 0}]:
            with self.subTest(values=values):
                # Separate evidence directories prevent a previous synthetic TRX masking a missing report.
                self.artifacts = self.root / ("evidence-" + str(len(self.calls)))
                with self.assertRaises(RuntimeError):
                    scan.execute(self.root, self.artifacts, run=self.executor(**values))
                matrix = json.loads((self.artifacts / "matrix.json").read_text())
                self.assertTrue(all(entry["execution"] == "started" for entry in matrix))

    def test_execution_timeout_keeps_started_matrix_and_partial_log(self):
        successful_discovery = self.executor()
        def fail(command, **options):
            if "--list-tests" in command:
                return successful_discovery(command, **options)
            options["stdout"].write(b"partial execution")
            raise subprocess.TimeoutExpired(command, 1200)
        with self.assertRaises(subprocess.TimeoutExpired):
            scan.execute(self.root, self.artifacts, run=fail)
        matrix = json.loads((self.artifacts / "matrix.json").read_text())
        self.assertTrue(all(entry["execution"] == "started" for entry in matrix))
        self.assertEqual(b"partial execution", (self.artifacts / "execution.log").read_bytes())

    def test_discovery_only_is_explicitly_unexecuted(self):
        scan.execute(self.root, self.artifacts, discover_only=True, run=self.executor())
        matrix = json.loads((self.artifacts / "matrix.json").read_text())
        self.assertTrue(all(entry["execution"] == "not-run" for entry in matrix))
        self.assertEqual(1, len(self.calls))
        self.assertFalse((self.artifacts / "trace-gas.trx").exists())

    def test_planned_or_empty_contract_cannot_be_an_execution_promise(self):
        original = self.contract.read_text(encoding="utf-8")
        for text in [original.replace("verification entry points ✅", "verification entry points ⏳"),
                     original.replace("public void ", "private void "),
                     original.replace("```csharp", "### Planned ⏳\n\n```csharp")]:
            with self.subTest(text=text):
                self.contract.write_text(text, encoding="utf-8")
                with self.assertRaises(RuntimeError):
                    scan.documented_methods(self.root)

    def test_discovery_timeout_preserves_partial_raw_log(self):
        def fail(command, **options):
            options["stdout"].write(b"partial discovery")
            raise subprocess.TimeoutExpired(command, 120)
        with self.assertRaises(subprocess.TimeoutExpired):
            scan.execute(self.root, self.artifacts, run=fail)
        self.assertEqual(b"partial discovery", (self.artifacts / "discovery.log").read_bytes())


class TraceWorkflowGuards(unittest.TestCase):
    def test_each_affected_event_is_hosted_and_collects_scan_evidence(self):
        text = (scan.ROOT / ".github/workflows/trace-gas.yml").read_text(encoding="utf-8")
        for event in ["push", "pull_request"]:
            # Parse the event block by its actual indentation, not a hand-written per-event path copy.
            lines = text.split("  " + event + ":\n", 1)[1].splitlines()
            paths = []
            for line in lines:
                if line.startswith("  ") and not line.startswith("    "):
                    break
                if line.startswith("      - "):
                    paths.append(line.strip()[2:].strip("'\""))
            self.assertTrue(paths, event)
            for affected in ["src/Equilibrium/TraceGas/Rule.cs", "src/Equilibrium/Recovery/Rule.cs",
                             "tests/Equilibrium.Tests/Scan.cs", "tests/Fixtures/tolerances.json",
                             "Directory.Build.props", ".github/scripts/trace_scan.py"]:
                self.assertTrue(any(fnmatch.fnmatchcase(affected, pattern) for pattern in paths), affected)
        self.assertIn("workflow_dispatch:", text)
        self.assertNotIn("self-hosted", text)
        self.assertNotIn("schedule:", text)
        self.assertIn("os: [windows-latest, ubuntu-latest]", text)
        self.assertIn("APTHERMO_NO_CUDA: '1'", text)
        self.assertIn("if: always()", text)
        self.assertIn("if-no-files-found: error", text)
        self.assertIn(".github/scripts/trace_scan.py --results-dir artifacts/trace-gas", text)


if __name__ == "__main__":
    unittest.main(verbosity=2)
