#!/usr/bin/env python3
"""Bounded controls for hosted project coverage, evidence and actual workflow wiring."""
import contextlib
import io
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

import hosted_tests as hosted


def hosted_job(text, job):
    match = re.search(r"^  " + job + r":\n(.*?)(?=^  [\w-]+:|\Z)", text, re.M | re.S)
    if not match:
        raise AssertionError("missing hosted job " + job)
    return match[1]


def workflow_steps(body):
    matches = list(re.finditer(r"^      - name: (.+)$", body, re.M))
    result = {}
    for index, match in enumerate(matches):
        name = match[1]
        if name in result:
            raise AssertionError("duplicate step " + name)
        end = matches[index + 1].start() if index + 1 < len(matches) else len(body)
        result[name] = body[match.end():end]
    return result


def verify_wiring(text, job):
    body = hosted_job(text, job)
    active = "\n".join(line for line in body.splitlines() if not line.lstrip().startswith("#"))
    if re.search(r"(?m)^\s*continue-on-error\s*:", body):
        raise AssertionError("hosted job and steps cannot mask failures")
    if re.search(r"(?im)(?:-p:|/p:|--property:)(?:NoWarn|WarningsNotAsErrors|MSBuildWarningsAsMessages|MSBuildWarningsNotAsErrors)\s*=", body):
        raise AssertionError("hosted commands cannot suppress diagnostics")
    if re.search(r"(?m)^.*?(?:\|\|\s*(?:true|exit\s+0)|--discover-only|\bset\s+\+e\b)", active):
        raise AssertionError("hosted commands must execute and preserve failure")
    for key, expected in (("os", ["windows-latest", "ubuntu-latest"]), ("lane", list(hosted.LANES))):
        match = re.search(r"^        " + key + r": \[([^\]]*)\]$", body, re.M)
        if match is None or [item.strip() for item in match[1].split(",")] != expected:
            raise AssertionError("wrong hosted " + key + " matrix")
    if "name: ${{ matrix.os }} — ${{ matrix.lane }} build & test" not in body:
        raise AssertionError("hosted job name must distinguish both axes")
    if re.search(r"^        (include|exclude):", body, re.M):
        raise AssertionError("hosted matrix may not silently add or omit lane combinations")
    if "    runs-on: ${{ matrix.os }}\n" not in body or "      fail-fast: false\n" not in body:
        raise AssertionError("all hosted lane jobs must run independently on their selected OS")
    steps = workflow_steps(body)
    runner = steps["Hosted test lane"]
    command = "run: python -X utf8 .github/scripts/hosted_tests.py --lane ${{ matrix.lane }} --results-dir artifacts/hosted-tests"
    if ("        " + command + "\n") not in runner or body.count(".github/scripts/hosted_tests.py --lane") != 1:
        raise AssertionError("every lane must call the single runner exactly once")
    if "APTHERMO_NO_CUDA: '1'" not in runner or "        if:" in runner:
        raise AssertionError("all hosted lanes must run on CPU unconditionally")
    if "run: python -X utf8 .github/scripts/test_hosted_tests.py" not in steps["Hosted runner guard tests"]:
        raise AssertionError("actual hosted wiring must run its guards")
    documents = steps.get("Fixture document-comparison guard tests", "")
    if ("        run: python -X utf8 tests/Fixtures/generate/test_document_comparison.py\n" not in documents
            or "        if:" in documents):
        raise AssertionError("fixture document-comparison guards must run unconditionally")
    if "dotnet test " in body or "        run: dotnet build APThermo.sln --configuration Release\n" not in steps["Build"]:
        raise AssertionError("hosted tests must use one runner after building the solution")
    artifacts = steps["Upload hosted test evidence"]
    for required in ("if: always()", "if-no-files-found: error", "path: artifacts/hosted-tests",
                     "name: hosted-tests-${{ matrix.os }}-${{ matrix.lane }}"):
        if required not in artifacts:
            raise AssertionError("missing nonempty lane evidence wiring: " + required)
    if "name: bit-snapshot-diagnostics-${{ matrix.os }}-${{ matrix.lane }}" not in steps["Upload bit-snapshot diagnostics"]:
        raise AssertionError("diagnostic artifacts collide across lanes")
    if job == "matrix":
        if "    timeout-minutes: 30\n" not in body:
            raise AssertionError("release hosted timeout changed")
        if "    needs: matrix\n" not in hosted_job(text, "cuda-windows"):
            raise AssertionError("CUDA must wait for every hosted lane")
        return
    for name in ("Determine the package version", "Pack both packages", "Samples run against the packaged library",
                 "Install the packed tool and run an approved example"):
        if "        if: matrix.lane == 'core'\n" not in steps[name]:
            raise AssertionError("package consumption must run once per OS in core: " + name)
    fixture = steps["Fixtures are bound to the generator"]
    if ("        if: matrix.os == 'windows-latest' && matrix.lane == 'core'\n" not in fixture
            or "          .venv-fixtures/Scripts/python tests/Fixtures/generate/regenerate.py --check --sample\n" not in fixture):
        raise AssertionError("binding command must run once in Windows core")
    if "        if: matrix.os == 'ubuntu-latest' && matrix.lane == 'core'\n" not in steps["Upload packages"]:
        raise AssertionError("packages upload must run once in Ubuntu core")


def verify_command(command):
    required = ["--configuration", "Release", "--no-build", "-m:1", "--filter", "Category!=LongRunning",
                "--logger", "trx;LogFileName=tests.trx", "--results-directory"]
    if command[:2] != ["dotnet", "test"] or command[3:12] != required:
        raise AssertionError("hosted test population, configuration or serialization changed")
    if len(command) != 13:
        raise AssertionError("unexpected hosted test argument")


class HostedRunnerGuards(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name).resolve()
        self.artifacts = self.root / "evidence"
        self.projects = [hosted.CLI, hosted.DOCS, "tests/First.Tests/APThermo.First.Tests.csproj",
                         "tests/Second.Tests/APThermo.Second.Tests.csproj", "src/Product/APThermo.Product.csproj"]
        for project in self.projects:
            path = self.root / project
            path.parent.mkdir(parents=True, exist_ok=True)
            path.touch()
        self.calls = []
        self.snapshots = []

    def report(self, path, outcome="Passed", total=2, passed=2, count=2):
        root = ET.Element("TestRun", xmlns=hosted.NS["t"])
        results = ET.SubElement(root, "Results")
        for index in range(count):
            ET.SubElement(results, "UnitTestResult", executionId=str(index), outcome=outcome)
        summary = ET.SubElement(root, "ResultSummary", outcome="Completed")
        ET.SubElement(summary, "Counters", total=str(total), executed=str(total), passed=str(passed), failed="0", notExecuted="0")
        ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)

    def fake(self, discovery=None, discovery_exit=0, behavior=None):
        def run(command, **options):
            self.calls.append(command)
            self.assertEqual(self.root, options["cwd"])
            if command[0] == "git":
                data = b"a" * 40 if command[1] == "rev-parse" else b""
                return subprocess.CompletedProcess(command, 0, stdout=data)
            if command[1] == "sln":
                found = self.projects if discovery is None else discovery
                options["stdout"].write(("Project(s)\n----------\n" + "\n".join(found) + "\n").encode())
                return subprocess.CompletedProcess(command, discovery_exit)
            verify_command(command)
            self.assertEqual("1", options["env"]["APTHERMO_NO_CUDA"])
            manifest = json.loads((self.artifacts / "manifest.json").read_text())
            current = next(record for record in manifest["executions"] if record["command"] == command)
            self.assertEqual("started", current["status"], "started must be persisted BEFORE the real invocation")
            self.snapshots.append(manifest)
            options["stdout"].write(b"controlled output\n")
            report = Path(command[-1]) / "tests.trx"
            if behavior:
                return behavior(command, report)
            self.report(report)
            return subprocess.CompletedProcess(command, 0)
        return run

    def execute(self, lane="core", **options):
        # Replay is a console concern; assertions inspect the actual preserved raw bytes.
        with contextlib.redirect_stdout(io.TextIOWrapper(io.BytesIO(), encoding="utf-8")):
            code = hosted.execute(self.root, self.artifacts, lane, run=self.fake(**options))
        return code, json.loads((self.artifacts / "manifest.json").read_text())

    def test_every_lane_executes_exactly_its_assignment_with_honest_timing_and_logs(self):
        for lane in hosted.LANES:
            with self.subTest(lane=lane):
                code, manifest = self.execute(lane)
                self.assertEqual(0, code)
                self.assertEqual("passed", manifest["status"])
                self.assertEqual(manifest["assignments"][lane], [record["project"] for record in manifest["executions"]])
                self.assertEqual("a" * 40, manifest["source"]["commit"])
                for record in manifest["executions"]:
                    self.assertEqual("passed", record["status"])
                    self.assertEqual(0, record["exit_code"])
                    self.assertEqual(2, record["counters"]["passed"])
                    self.assertGreaterEqual(record["elapsed_seconds"], 0)
                    self.assertLessEqual(record["started"]["utc"], record["finished"]["utc"])
                    self.assertEqual(b"controlled output\n", (Path(record["results_directory"]) / "execution.log").read_bytes())
                self.assertEqual(["dotnet", "sln", "APThermo.sln", "list"], [call for call in self.calls if call[1] == "sln"][-1])
                self.assertEqual(len(manifest["selected_projects"]), len({record["project"] for record in manifest["executions"]}))

    def test_new_solution_test_project_automatically_joins_core(self):
        project = "tests/New.Tests/APThermo.New.Tests.csproj"
        path = self.root / project
        path.parent.mkdir(parents=True)
        path.touch()
        self.projects.append(project)
        code, manifest = self.execute()
        self.assertEqual(0, code)
        self.assertIn(project, manifest["selected_projects"])
        self.assertEqual(3, len(manifest["executions"]))

    def test_solution_cannot_omit_a_test_project_present_in_tree(self):
        missing = "tests/First.Tests/APThermo.First.Tests.csproj"
        code, manifest = self.execute(discovery=[p for p in self.projects if p != missing])
        self.assertEqual(1, code)
        self.assertEqual("failed", manifest["status"])
        self.assertEqual([], manifest["executions"])

    def test_discovery_rejects_empty_duplicate_outside_missing_and_missing_designations(self):
        invalid = [[], self.projects + [hosted.CLI], self.projects + ["../Outside.Tests.csproj"],
                   self.projects + ["C:/outside/Outside.Tests.csproj"],
                   self.projects + ["tests/Missing.Tests/Missing.Tests.csproj"], self.projects[1:], self.projects[:1] + self.projects[2:]]
        for found in invalid:
            with self.subTest(found=found):
                code, manifest = self.execute(discovery=found)
                self.assertEqual(1, code)
                self.assertEqual("failed", manifest["status"])
                self.assertEqual([], manifest["executions"])
                self.assertEqual("failed", manifest["discovery"]["status"])

    def test_nonzero_discovery_never_executes_tests(self):
        code, manifest = self.execute(discovery_exit=1)
        self.assertEqual(1, code)
        self.assertEqual([], manifest["executions"])

    def test_backslash_solution_paths_are_normalized(self):
        code, manifest = self.execute(discovery=[project.replace("/", "\\") for project in self.projects])
        self.assertEqual(0, code)
        self.assertEqual(sorted(self.projects), manifest["solution_projects"])

    def test_partition_controls_reject_duplicates_gaps_wrong_owners_and_empty_lanes(self):
        projects = sorted(project for project in self.projects if project.endswith(".Tests.csproj"))
        valid = hosted.assignments(projects)
        for mutation in ("duplicate", "gap", "owner", "empty", "missing-lane"):
            lanes = json.loads(json.dumps(valid))
            if mutation == "duplicate":
                lanes["core"].append(hosted.CLI)
            elif mutation == "gap":
                lanes["core"].pop()
            elif mutation == "owner":
                lanes["cli"], lanes["docs"] = lanes["docs"], lanes["cli"]
            elif mutation == "empty":
                lanes["core"] = []
            else:
                del lanes["docs"]
            with self.subTest(mutation=mutation), self.assertRaises(RuntimeError):
                hosted.verify_assignments(projects, lanes)

    def test_failures_continue_other_projects_and_never_report_passed(self):
        cases = ("command", "missing", "empty", "failed", "skipped", "inconsistent", "duplicate", "malformed", "oserror", "timeout")
        for case in cases:
            with self.subTest(case=case):
                attempts = []
                def behavior(command, report):
                    attempts.append(command)
                    if len(attempts) > 1:
                        self.report(report)
                        return subprocess.CompletedProcess(command, 0)
                    if case == "timeout":
                        raise subprocess.TimeoutExpired(command, 5)
                    if case == "oserror":
                        raise OSError("controlled process launch failure")
                    if case == "empty":
                        report.touch()
                    elif case == "malformed":
                        report.write_text("not XML")
                    elif case != "missing":
                        self.report(report, outcome={"failed": "Failed", "skipped": "NotExecuted"}.get(case, "Passed"),
                                    total=3 if case == "inconsistent" else 2)
                        if case == "duplicate":
                            text = report.read_text().replace('executionId="1"', 'executionId="0"')
                            report.write_text(text)
                    return subprocess.CompletedProcess(command, 7 if case == "command" else 0)
                code, manifest = self.execute(behavior=behavior)
                self.assertEqual(1, code)
                self.assertEqual(2, len(attempts))
                self.assertEqual(["failed", "passed"], [record["status"] for record in manifest["executions"]])
                self.assertEqual("failed", manifest["status"])
                self.assertTrue(all("started" in record and "finished" in record for record in manifest["executions"]))
                if case == "inconsistent":
                    self.assertEqual(3, manifest["executions"][0]["counters"]["total"])

    def test_discovery_timeout_preserves_partial_log_and_failed_state(self):
        normal = self.fake()
        def run(command, **options):
            if command[0] == "dotnet":
                options["stdout"].write(b"partial discovery")
                raise subprocess.TimeoutExpired(command, 120)
            return normal(command, **options)
        code = hosted.execute(self.root, self.artifacts, "core", run=run)
        manifest = json.loads((self.artifacts / "manifest.json").read_text())
        self.assertEqual(1, code)
        self.assertEqual("failed", manifest["discovery"]["status"])
        self.assertGreaterEqual(manifest["discovery"]["elapsed_seconds"], 0)
        self.assertEqual(b"partial discovery", (Path(manifest["attempt"]) / "discovery.log").read_bytes())
        self.assertEqual([], manifest["executions"])

    def test_zero_exit_cannot_override_failed_skipped_or_incomplete_summary(self):
        for attribute, value in (("failed", "1"), ("notExecuted", "1"), ("passed", "1"), ("total", "0")):
            with self.subTest(attribute=attribute):
                def behavior(command, report):
                    self.report(report)
                    document = ET.parse(report)
                    document.find(".//t:Counters", hosted.NS).set(attribute, value)
                    document.write(report, encoding="utf-8", xml_declaration=True)
                    return subprocess.CompletedProcess(command, 0)
                code, manifest = self.execute(behavior=behavior)
                self.assertEqual(1, code)
                self.assertTrue(all(record["status"] == "failed" for record in manifest["executions"]))
                self.assertEqual(int(value), manifest["executions"][0]["counters"][attribute])

    def test_unavailable_or_invalid_source_identity_cannot_be_reported_as_a_pass(self):
        for code, output in ((1, b"git failure"), (0, b"not-a-commit")):
            def run(command, **options):
                return subprocess.CompletedProcess(command, code, stdout=output)
            result = hosted.execute(self.root, self.artifacts, "core", run=run)
            manifest = json.loads((self.artifacts / "manifest.json").read_text())
            self.assertEqual(1, result)
            self.assertEqual("failed", manifest["status"])
            self.assertEqual([], manifest["executions"])

    def test_reusing_results_preserves_old_reports_but_cannot_accept_them(self):
        code, first = self.execute()
        old = Path(first["executions"][0]["results_directory"]) / "tests.trx"
        previous = old.read_bytes()
        code, second = self.execute(behavior=lambda command, report: subprocess.CompletedProcess(command, 0))
        self.assertEqual(1, code)
        self.assertNotEqual(first["attempt"], second["attempt"])
        self.assertEqual(previous, old.read_bytes())
        self.assertTrue(all(record["status"] == "failed" for record in second["executions"]))


class HostedWorkflowGuards(unittest.TestCase):
    def test_workflows_reject_masking_and_changed_runner_commands(self):
        for file, job in (("ci.yml", "build"), ("release.yml", "matrix")):
            text = (hosted.ROOT / ".github/workflows" / file).read_text(encoding="utf-8-sig")
            changes = [text.replace("    runs-on: ${{ matrix.os }}", "    continue-on-error: true\n    runs-on: ${{ matrix.os }}", 1),
                       text.replace("        run: dotnet build APThermo.sln --configuration Release", "        run: |\n          dotnet build APThermo.sln --configuration Release || true", 1),
                       text.replace("        run: dotnet build APThermo.sln --configuration Release", "        run: |\n          set +e\n          dotnet build APThermo.sln --configuration Release", 1),
                       text.replace("dotnet build APThermo.sln --configuration Release", "dotnet build APThermo.sln --configuration Release -p:NoWarn=CS1591", 1),
                       text.replace("--results-dir artifacts/hosted-tests", "--results-dir artifacts/hosted-tests --discover-only", 1),
                       text.replace("--results-dir artifacts/hosted-tests", "--results-dir artifacts/hosted-tests || true", 1)]
            for changed in changes:
                with self.subTest(file=file, changed=changed == text):
                    self.assertNotEqual(text, changed)
                    with self.assertRaises(AssertionError):
                        verify_wiring(changed, job)
    def test_fixture_and_sample_commands_cannot_be_replaced_or_masked(self):
        text = (hosted.ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8-sig")
        fixture = ".venv-fixtures/Scripts/python tests/Fixtures/generate/regenerate.py --check --sample"
        comparison = 'diff actual-sample.txt "tests/Docs.Tests/approved/samples/$approved"'
        for original, replacement in ((fixture, "echo fixture"), (comparison, comparison + " || true")):
            self.assertIn(original, text)
            with self.subTest(original=original), self.assertRaises(AssertionError):
                verify_wiring(text.replace(original, replacement, 1), "build")

    def test_actual_workflows_have_all_lanes_and_correct_artifact_package_and_binding_conditions(self):
        for file, job in (("ci.yml", "build"), ("release.yml", "matrix")):
            with self.subTest(file=file):
                verify_wiring((hosted.ROOT / ".github/workflows" / file).read_text(encoding="utf-8-sig"), job)

    def test_fixture_document_comparison_guards_run_in_both_workflows(self):
        for file, job in (("ci.yml", "build"), ("release.yml", "matrix")):
            text = (hosted.ROOT / ".github/workflows" / file).read_text(encoding="utf-8-sig")
            self.assertIn("\n        run: python -X utf8 tests/Fixtures/generate/test_document_comparison.py\n",
                          workflow_steps(hosted_job(text, job))["Fixture document-comparison guard tests"])
            step = "      - name: Fixture document-comparison guard tests\n"
            self.assertIn(step, text)
            with self.assertRaises(AssertionError):
                verify_wiring(text.replace(step, step + "        if: false\n", 1), job)
            with self.assertRaises(AssertionError):
                verify_wiring(text.replace(step, "      - name: Removed document-comparison guard\n", 1), job)

    def test_actual_command_preserves_test_population_and_serial_execution(self):
        verify_command(hosted.test_command(hosted.ROOT, hosted.CLI, Path("evidence")))

    def test_workflow_guard_is_red_for_lane_filter_condition_and_artifact_mutations(self):
        text = (hosted.ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8-sig")
        mutations = [text.replace("lane: [core, cli, docs]", "lane: [core, cli, docs]\n        exclude: [{os: windows-latest, lane: docs}]"),
                     text.replace("lane: [core, cli, docs]", "lane: [core, cli]"),
                     text.replace("lane: [core, cli, docs]", "lane: [core, cli, cli]"),
                     text.replace("--lane ${{ matrix.lane }}", "--lane core"),
                     text.replace("if: matrix.lane == 'core'", "if: matrix.lane == 'cli'"),
                     text.replace("if: matrix.os == 'windows-latest' && matrix.lane == 'core'", "if: matrix.os == 'windows-latest'"),
                     text.replace("if: matrix.os == 'ubuntu-latest' && matrix.lane == 'core'", "if: matrix.os == 'ubuntu-latest'"),
                     text.replace("hosted-tests-${{ matrix.os }}-${{ matrix.lane }}", "hosted-tests-${{ matrix.os }}"),
                     text.replace("bit-snapshot-diagnostics-${{ matrix.os }}-${{ matrix.lane }}", "bit-snapshot-diagnostics-${{ matrix.os }}"),
                     text.replace("if: always()", "if: success()")]
        for index, changed in enumerate(mutations):
            with self.subTest(index=index):
                self.assertTrue(text != changed, "mutation must change the source")
                with self.assertRaises(AssertionError):
                    verify_wiring(changed, "build")
        command = hosted.test_command(hosted.ROOT, hosted.CLI, Path("evidence"))
        for changed in (command[:8] + command[10:], ["Category!=EndToEnd" if arg == "Category!=LongRunning" else arg for arg in command],
                        [arg for arg in command if arg != "-m:1"]):
            with self.assertRaises(AssertionError):
                verify_command(changed)

    def test_release_timeout_and_cuda_dependency_mutations_are_red(self):
        text = (hosted.ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8-sig")
        for changed in (text.replace("timeout-minutes: 30", "timeout-minutes: 60"), text.replace("needs: matrix", "needs: check")):
            with self.assertRaises(AssertionError):
                verify_wiring(changed, "matrix")


if __name__ == "__main__":
    unittest.main(verbosity=2)
