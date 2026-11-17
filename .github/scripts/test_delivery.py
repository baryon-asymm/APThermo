#!/usr/bin/env python3
"""Controlled local guards; actual GitHub runner proof is separate delivery evidence."""
import base64
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / ".github" / "scripts"
RELEASE = ROOT / ".github" / "workflows" / "release.yml"


def bash_path():
    if os.name == "nt":
        git = shutil.which("git")
        candidate = Path(git).parents[1] / "bin" / "bash.exe" if git else Path("missing-bash")
        if candidate.is_file():
            return str(candidate)
    found = shutil.which("bash")
    if not found:
        raise RuntimeError("Bash is required for delivery guards")
    return found


def step_lines(name):
    lines = RELEASE.read_text(encoding="utf-8").splitlines()
    start = next(i for i, line in enumerate(lines) if line.strip() == "- name: " + name)
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("      - name:")), len(lines))
    return lines[start:end]


def step_script(name):
    body = step_lines(name)
    first = next(i for i, line in enumerate(body) if line.strip() == "run: |") + 1
    return "\n".join(line[10:] for line in body[first:] if line.startswith("          ")) + "\n"


def step_environment(name, ref, event="push", version="0.3.0"):
    values = {"${{ github.ref_name }}": ref, "${{ github.event_name }}": event,
              "${{ steps.version.outputs.version }}": version}
    result = {}
    for line in step_lines(name):
        if line.startswith("          RELEASE_"):
            key, value = line.strip().split(":", 1)
            result[key] = values.get(value.strip(), value.strip())
    return result


def gpu_job(text, name):
    starts = list(re.finditer(r"(?m)^  " + re.escape(name) + r": *$", text))
    if len(starts) != 1:
        return ""
    remaining = text[starts[0].end():]
    following = re.search(r"(?m)^  [a-z][a-z0-9-]*: *$", remaining)
    return remaining[:following.start()] if following else remaining


def gpu_step(job, name):
    starts = list(re.finditer(r"(?m)^      - name: " + re.escape(name) + r" *$", job))
    if len(starts) != 1:
        return ""
    remaining = job[starts[0].end():]
    following = re.search(r"(?m)^      - name: ", remaining)
    return remaining[:following.start()] if following else remaining


def one_value(text, key, indent):
    values = re.findall(r"(?m)^" + " " * indent + re.escape(key) + r": *([^\r\n]+)$", text)
    return values[0].strip() if len(values) == 1 else ""


SWEEP_FACTS = (
    "APThermo.Execution.Tests.CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic",
    "APThermo.Execution.Tests.CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio",
)
REMAINDER_FILTER = "(Category=Cuda|Category=BitSnapshot)" + "".join(
    "&FullyQualifiedName!=" + fact for fact in SWEEP_FACTS)
SWEEP_FILTER = "|".join("FullyQualifiedName=" + fact for fact in SWEEP_FACTS)
SWEEP_CONDITION = "${{ !cancelled() && steps.build.outcome == 'success' }}"
GPU_PHASES = ("CUDA tests", "CUDA sweep checks")


def gpu_arguments(phase, variable):
    project = "APThermo.sln" if phase == "CUDA tests" else "tests/Execution.Tests/APThermo.Execution.Tests.csproj"
    selection = REMAINDER_FILTER if phase == "CUDA tests" else SWEEP_FILTER
    arguments = ["dotnet", "test", project, "--configuration", "Release", "--no-build", "-m:1",
                 "--filter", selection, "--logger", "trx;LogFileName=" + variable]
    return arguments if phase == "CUDA tests" else arguments + ["--", "RunConfiguration.TreatNoTestsAsError=true"]


def sweep_source_violations(text):
    """Inspect only named declaration/attribute metadata, never test bodies."""
    problems = []
    for fact in SWEEP_FACTS:
        name = fact.rsplit(".", 1)[1]
        declarations = re.findall(r'((?:    \[[^\n]+\]\n)+)    public void ' + re.escape(name) + r'\(\)', text)
        if len(declarations) != 1 or '[Fact]' not in declarations[0] or '[Trait("Category", "Cuda")]' not in declarations[0]:
            problems.append("missing or changed original source fact: " + fact)
    return problems


def gpu_release_violations(text):
    """Read both actual job blocks; a configuration guard never starts a tool."""
    violations = {}
    for name, shell, platform, needs, variable in [
        ("cuda-windows", "powershell", "windows", "matrix", "$env:GPU_TEST_TRX"),
        ("cuda-linux", "bash", "linux", "[matrix,cuda-windows]", "$GPU_TEST_TRX"),
    ]:
        problems = violations[name] = []
        job = gpu_job(text, name)
        if not job:
            problems.append("missing or duplicate GPU job")
            continue
        if one_value(job, "needs", 4).replace(" ", "") != needs:
            problems.append("sequential dependency changed")
        if one_value(job, "timeout-minutes", 4) != "180":
            problems.append("GPU job limit changed")
        if one_value(job, "runs-on", 4) != "['self-hosted', '" + platform + "', 'gpu']":
            problems.append("GPU runner class changed")
        for key, suffix in [("GPU_TEST_TRX", ""), ("GPU_SWEEP_TRX", "-sweep")]:
            if one_value(job, key, 6) != "gpu-${{ github.run_id }}-${{ github.run_attempt }}" + suffix + ".trx":
                problems.append(key + " identity is not distinct/run/attempt-specific")
        checkout = gpu_step(job, "Checkout")
        if one_value(checkout, "persist-credentials", 10) != "false" or one_value(checkout, "clean", 10) != "true":
            problems.append("fresh credential-free checkout changed")
        build = gpu_step(job, "Build")
        if one_value(build, "shell", 8) != shell or one_value(build, "id", 8) != "build":
            problems.append("explicit GPU build shell/id changed")
        if one_value(build, "run", 8) != "dotnet build APThermo.sln --configuration Release" or one_value(build, "if", 8):
            problems.append("successful unconditional build changed")
        if len(re.findall(r"\bdotnet\s+test\b", job)) != 2:
            problems.append("GPU job must contain exactly two test invocations")
        if re.search(r"(?m)^\s*continue-on-error:", job):
            problems.append("GPU failure masking is forbidden")
        for phase, key in zip(GPU_PHASES, ["GPU_TEST_TRX", "GPU_SWEEP_TRX"]):
            step = gpu_step(job, phase)
            if one_value(step, "shell", 8) != shell:
                problems.append(phase + ": explicit GPU shell changed")
            condition = "" if phase == "CUDA tests" else SWEEP_CONDITION
            if one_value(step, "if", 8) != condition:
                problems.append(phase + ": post-failure/noncancelled build condition changed")
            try:
                arguments = shlex.split(one_value(step, "run", 8))
            except ValueError:
                arguments = []
            prefix = "$env:" if shell == "powershell" else "$"
            if arguments != gpu_arguments(phase, prefix + key):
                problems.append(phase + ": selection/serialization/native refusal/logger command changed")
        if job.find("- name: Build") > job.find("- name: CUDA tests") or job.find("- name: CUDA tests") > job.find("- name: CUDA sweep checks"):
            problems.append("build/remainder/fresh-pair order changed")
        discovery = gpu_step(job, "Discover GPU evidence inventory")
        verifier = gpu_step(job, "Verify GPU evidence inventory")
        sdk_setup = "$env:GPU_EVIDENCE_SDK = dotnet --version" if shell == "powershell" else 'export GPU_EVIDENCE_SDK="$(dotnet --version)"'
        discover_command = "dotnet run --project tests/GpuEvidence --configuration Release -- discover artifacts/gpu-evidence/inventory.json"
        discover_lines = [line.strip() for line in discovery.splitlines() if line.startswith("          ")]
        if one_value(discovery, "shell", 8) != shell or one_value(discovery, "if", 8) or discover_lines != [sdk_setup, discover_command]:
            problems.append("GPU inventory discovery is missing or changed")
        prefix = "$env:" if shell == "powershell" else "$"
        names = prefix + "GPU_TEST_TRX " + prefix + "GPU_SWEEP_TRX" if shell == "powershell" else '"$GPU_TEST_TRX" "$GPU_SWEEP_TRX"'
        verify_command = "dotnet run --project tests/GpuEvidence --configuration Release --no-build -- verify artifacts/gpu-evidence/inventory.json " + names
        if one_value(verifier, "shell", 8) != shell or one_value(verifier, "if", 8) != SWEEP_CONDITION or one_value(verifier, "run", 8) != verify_command:
            problems.append("GPU inventory verification is missing or changed")
        if not (job.find("- name: Build") < job.find("- name: Discover GPU evidence inventory") < job.find("- name: CUDA tests") < job.find("- name: CUDA sweep checks") < job.find("- name: Verify GPU evidence inventory") < job.find("- name: Upload GPU test evidence")):
            problems.append("GPU inventory step order changed")
        upload = gpu_step(job, "Upload GPU test evidence")
        if one_value(upload, "if", 8) != "always()" or one_value(upload, "uses", 8) != "actions/upload-artifact@v7":
            problems.append("GPU evidence is not always uploaded")
        expected_name = "gpu-tests-" + platform + "-${{ github.run_id }}-${{ github.run_attempt }}"
        if one_value(upload, "name", 10) != expected_name:
            problems.append("GPU artifact identity is not OS/run/attempt-specific")
        if one_value(upload, "if-no-files-found", 10) != "error":
            problems.append("missing GPU evidence does not fail")
        paths = [line.strip() for line in upload.splitlines() if line.startswith("            ")]
        if paths != ["tests/**/TestResults/${{ env.GPU_TEST_TRX }}",
                     "tests/Execution.Tests/TestResults/${{ env.GPU_SWEEP_TRX }}",
                     "tests/Execution.Tests/Throughput*.actual.txt",
                     "artifacts/gpu-evidence/inventory.json"]:
            problems.append("per-project TRX or throughput evidence changed")
    if "  pull_request:" in text or "    branches:" in text:
        for problems in violations.values():
            problems.append("self-hosted release admits an unapproved event")
    return violations


def gpu_job_mutations(job, name):
    """Independent defects in each actual phase, retained for red-control evidence."""
    shell = "powershell" if name == "cuda-windows" else "bash"
    mutations = {}
    for phase in GPU_PHASES:
        step = gpu_step(job, phase)
        changes = {"serialization removed": (" -m:1", ""), "serialization widened": (" -m:1", " -m:2"),
                   "Release lost": ("--configuration Release", "--configuration Debug"),
                   "no-build lost": (" --no-build", ""), "shell implicit": ("        shell: " + shell, ""),
                   "logger lost": ("--logger", "--verbosity"), "failure masked": ("        run: dotnet", "        continue-on-error: true\n        run: dotnet")}
        for label, (before, after) in changes.items():
            mutations[phase + ": " + label] = job.replace(step, step.replace(before, after, 1), 1)
        mutations[phase + ": omitted"] = job.replace("      - name: " + phase + step, "", 1)
        mutations[phase + ": duplicated"] = job.replace("      - name: " + phase + step, ("      - name: " + phase + step) * 2, 1)
    remainder = gpu_step(job, "CUDA tests")
    pair = gpu_step(job, "CUDA sweep checks")
    for fact in SWEEP_FACTS:
        mutations["remainder: exclusion omitted " + fact] = job.replace("&FullyQualifiedName!=" + fact, "", 1)
        mutations["remainder: exclusion widened " + fact] = job.replace("FullyQualifiedName!=" + fact, "FullyQualifiedName!~" + fact, 1)
        member = "FullyQualifiedName=" + fact
        reduced = SWEEP_FILTER.replace(member + "|", "").replace("|" + member, "")
        mutations["pair: member omitted " + fact] = job.replace(pair, pair.replace(SWEEP_FILTER, reduced), 1)
    changes = {
        "remainder: category narrowed": ("(Category=Cuda|Category=BitSnapshot)", "Category=Cuda"),
        "remainder: Boolean precedence": ("(Category=Cuda|Category=BitSnapshot)", "Category=Cuda|Category=BitSnapshot"),
        "remainder: pair duplicated": (REMAINDER_FILTER, "(Category=Cuda|Category=BitSnapshot)"),
        "pair: wrong project": ("test tests/Execution.Tests/APThermo.Execution.Tests.csproj", "test APThermo.sln"),
        "pair: wrong Boolean": (SWEEP_FILTER, SWEEP_FILTER.replace("|", "&")),
        "pair: condition missing": ("        if: " + SWEEP_CONDITION + "\n", ""),
        "pair: failure skipped": (SWEEP_CONDITION, "${{ success() }}"),
        "pair: cancellation ignored": (SWEEP_CONDITION, "${{ always() }}"),
        "pair: build outcome ignored": (SWEEP_CONDITION, "${{ !cancelled() }}"),
        "pair: empty accepted": (" -- RunConfiguration.TreatNoTestsAsError=true", ""),
        "pair: empty setting disabled": ("TreatNoTestsAsError=true", "TreatNoTestsAsError=false"),
        "build id lost": ("        id: build\n", ""),
        "dependency lost": ("    needs: matrix" if name == "cuda-windows" else "    needs: [matrix, cuda-windows]", "    needs: check"),
        "timeout raised": ("    timeout-minutes: 180", "    timeout-minutes: 181"),
        "remainder stale TRX": ("gpu-${{ github.run_id }}-${{ github.run_attempt }}.trx", "gpu.trx"),
        "pair colliding TRX": ("gpu-${{ github.run_id }}-${{ github.run_attempt }}-sweep.trx", "gpu-${{ github.run_id }}-${{ github.run_attempt }}.trx"),
        "checkout not fresh": ("          clean: true", "          clean: false"),
        "build shell implicit": ("        id: build\n        shell: " + shell, "        id: build"),
        "artifact collides": ("gpu-tests-windows-" if name == "cuda-windows" else "gpu-tests-linux-", "gpu-tests-"),
        "upload conditional": ("        if: always()", "        if: success()"),
        "missing evidence ignored": ("          if-no-files-found: error", "          if-no-files-found: ignore"),
        "remainder reports dropped": ("            tests/**/TestResults/${{ env.GPU_TEST_TRX }}\n", ""),
        "pair reports dropped": ("            tests/Execution.Tests/TestResults/${{ env.GPU_SWEEP_TRX }}\n", ""),
        "inventory omitted": ("      - name: Discover GPU evidence inventory", "      - name: Removed inventory"),
        "verification omitted": ("      - name: Verify GPU evidence inventory", "      - name: Removed verification"),
        "hidden extra test": ("      - name: Upload GPU test evidence", "      - name: Hidden test\n        shell: " + shell + "\n        run: |\n          dotnet test APThermo.sln\n\n      - name: Upload GPU test evidence"),
    }
    mutations.update({label: job.replace(before, after, 1) for label, (before, after) in changes.items()})
    mutations["pair: logger collides"] = job.replace(pair, pair.replace("GPU_SWEEP_TRX", "GPU_TEST_TRX"), 1)
    mutations["remainder: success forced"] = job.replace(remainder, remainder.rstrip() + " || true\n\n", 1)
    mutations["pair: before remainder"] = job.replace("      - name: CUDA tests" + remainder + "      - name: CUDA sweep checks" + pair,
                                                       "      - name: CUDA sweep checks" + pair + "      - name: CUDA tests" + remainder, 1)
    return mutations


def capture_gpu_arguments(command, name):
    with tempfile.TemporaryDirectory() as directory:
        environment = dict(os.environ, GPU_TEST_TRX="gpu-123-4.trx", GPU_SWEEP_TRX="gpu-123-4-sweep.trx")
        if name == "cuda-windows":
            shell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
            stub = Path(directory) / "dotnet.exe"
            source = ("using System; using System.Text; public static class CaptureArguments { "
                      "public static void Main(string[] args) { foreach (var argument in args) "
                      "Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(argument))); } }")
            environment["PATH"] = directory + os.pathsep + environment["PATH"]
            environment["EXPECTED_DOTNET"] = str(stub)
            script = ("Add-Type -TypeDefinition '" + source + "' -OutputType ConsoleApplication -OutputAssembly $env:EXPECTED_DOTNET\n"
                      "if ((Get-Command dotnet -CommandType Application | Select-Object -First 1).Source -ne $env:EXPECTED_DOTNET) { throw 'stub not selected' }\n" + command)
            result = subprocess.run([str(shell), "-NoProfile", "-NonInteractive", "-Command", script],
                                    cwd=directory, env=environment, text=True, capture_output=True, timeout=30)
            if result.returncode:
                raise AssertionError(result.stderr)
            return [base64.b64decode(line).decode("utf-8") for line in result.stdout.splitlines()]
        script = 'dotnet() { printf \'%s\\0\' "$@" > argv.bin; }\n' + command
        result = subprocess.run([bash_path(), "-c", script], cwd=directory, env=environment,
                                text=True, capture_output=True, timeout=30)
        if result.returncode:
            raise AssertionError(result.stderr)
        return (Path(directory) / "argv.bin").read_bytes().decode().split("\0")[:-1]


class GpuReleaseGuards(unittest.TestCase):
    def test_both_gpu_jobs_obey_the_release_contract(self):
        for name, problems in gpu_release_violations(RELEASE.read_text(encoding="utf-8")).items():
            with self.subTest(job=name):
                self.assertEqual([], problems)

    def test_original_shared_sweep_facts_still_exist(self):
        source = (ROOT / "tests/Execution.Tests/CudaTests.cs").read_text(encoding="utf-8-sig")
        self.assertEqual([], sweep_source_violations(source))
        for fact in SWEEP_FACTS:
            with self.subTest(fact=fact):
                self.assertTrue(sweep_source_violations(source.replace(fact.rsplit(".", 1)[1] + "()", "RemovedFact()", 1)))

    def test_each_gpu_job_rejects_command_shell_dependency_and_artifact_drift(self):
        text = RELEASE.read_text(encoding="utf-8")
        self.assertEqual({"cuda-windows": [], "cuda-linux": []}, gpu_release_violations(text))
        for name in ["cuda-windows", "cuda-linux"]:
            original = gpu_job(text, name)
            for mutation, changed_job in gpu_job_mutations(original, name).items():
                with self.subTest(job=name, mutation=mutation):
                    self.assertNotEqual(original, changed_job)
                    changed = text.replace(original, changed_job, 1)
                    problems = gpu_release_violations(changed)
                    self.assertTrue(problems[name], "mutation was not refused")
                    other = "cuda-linux" if name == "cuda-windows" else "cuda-windows"
                    self.assertEqual([], problems[other], "the guard confused the two GPU jobs")

    def test_available_shells_receive_the_exact_arguments(self):
        text = RELEASE.read_text(encoding="utf-8")
        jobs = ["cuda-linux"] + (["cuda-windows"] if os.name == "nt" else [])
        for name in jobs:
            for phase, variable in zip(GPU_PHASES, ["GPU_TEST_TRX", "GPU_SWEEP_TRX"]):
                with self.subTest(job=name, phase=phase):
                    command = one_value(gpu_step(gpu_job(text, name), phase), "run", 8)
                    filename = "gpu-123-4.trx" if phase == "CUDA tests" else "gpu-123-4-sweep.trx"
                    expected = gpu_arguments(phase, filename)[1:]
                    actual = capture_gpu_arguments(command, name)
                    print(json.dumps({"job": name, "phase": phase, "native_argv": actual}))
                    self.assertEqual(expected, actual)

    def test_release_refuses_pr_and_branch_push_execution(self):
        text = RELEASE.read_text(encoding="utf-8")
        self.assertEqual({"cuda-windows": [], "cuda-linux": []}, gpu_release_violations(text))
        for inserted in ["  pull_request:\n", "  push:\n    branches: ['**']\n"]:
            with self.subTest(event=inserted):
                self.assertTrue(all(gpu_release_violations(text.replace("on:\n", "on:\n" + inserted, 1)).values()))


class PublicationContractGuards(unittest.TestCase):
    @staticmethod
    def violations(text):
        required = {
            "pack": {"needs": "[cuda-windows,cuda-linux]", "if": ""},
            "publish": {"needs": "pack", "if": "github.event_name == 'push'"},
            "github-release": {"needs": "publish", "if": "github.event_name == 'push'"},
        }
        errors = []
        for name, values in required.items():
            job = gpu_job(text, name)
            for key, expected in values.items():
                actual = one_value(job, key, 4)
                if key == "needs":
                    actual = actual.replace(" ", "")
                if actual != expected:
                    errors.append(name + ":" + key)
        if one_value(gpu_job(text, "publish"), "environment", 4) != "release":
            errors.append("publish:environment")
        events = text.split("\nconcurrency:", 1)[0]
        if not re.search(r"(?m)^  push:\n    tags: \['v\*'\]$", events) or "  workflow_dispatch:" not in events or "  pull_request:" in events or "    branches:" in events:
            errors.append("release events")
        return errors

    def test_release_publication_chain_and_event_mutations(self):
        original = RELEASE.read_text(encoding="utf-8")
        self.assertEqual([], self.violations(original))
        for before, after in [("needs: [cuda-windows, cuda-linux]", "needs: cuda-windows"),
                              ("needs: pack", "needs: check"), ("needs: publish", "needs: pack"),
                              ("if: github.event_name == 'push'", "if: always()"),
                              ("tags: ['v*']", "branches: ['**']"),
                              ("  workflow_dispatch:", "  pull_request:")]:
            with self.subTest(mutation=before):
                self.assertIn(before, original)
                self.assertTrue(self.violations(original.replace(before, after, 1)))

    def test_publication_environment_is_required(self):
        original = RELEASE.read_text(encoding="utf-8")
        self.assertEqual([], self.violations(original))
        changed = original.replace("    environment: release\n", "", 1)
        self.assertNotEqual(original, changed)
        self.assertTrue(self.violations(changed))

    def test_ci_version_is_shell_environment_data(self):
        text = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        for name in ["Samples run against the packaged library", "Install the packed tool and run an approved example"]:
            step = gpu_step(text, name)
            self.assertEqual("${{ steps.version.outputs.version }}", one_value(step, "PACKAGE_VERSION", 10))
            self.assertIn('"$PACKAGE_VERSION"', step)
            self.assertEqual(1, step.count("${{ steps.version.outputs.version }}"))


class ReleaseGuards(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.bash = bash_path()
        target = self.root / ".github" / "scripts"
        target.mkdir(parents=True)
        shutil.copy2(SCRIPTS / "check-release.sh", target)

    def run_bash(self, script, environment=None):
        env = dict(os.environ)
        env.update(environment or {})
        return subprocess.run([self.bash, "-c", script], cwd=self.root, env=env,
                              text=True, capture_output=True, timeout=30)

    def extract(self, version, changelog, ref=""):
        (self.root / "CHANGELOG.md").write_text(changelog, encoding="utf-8")
        return self.run_bash('bash .github/scripts/check-release.sh "$VERSION" CHANGELOG.md notes.md "$REF"',
                             {"VERSION": version, "REF": ref})

    def test_exact_version_and_ref_extract_only_the_requested_section(self):
        result = self.extract("1.2.3+build.4", "## [1.2.3+build.4] - today\n\nCorrect notes.\n\n## [older]\nOther.\n", "v1.2.3+build.4")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("Correct notes.\n", (self.root / "notes.md").read_text())

    def test_version_dots_are_literal(self):
        result = self.extract("0.3.0", "## [0x3y0]\nWrong notes.\n")
        self.assertNotEqual(0, result.returncode)

    def test_empty_missing_and_wrong_tag_refuse(self):
        for text, ref in [("## [0.3.0]\n\n", ""), ("## [0.2.0]\nOther.\n", ""),
                          ("## [0.3.0]\nNotes.\n", "v0.3.1")]:
            with self.subTest(text=text, ref=ref):
                self.assertNotEqual(0, self.extract("0.3.0", text, ref).returncode)

    def test_adversarial_refs_are_valid_git_ref_names(self):
        for ref in ['v";touch${IFS}injected;#', 'v$(touch${IFS}injected)-"quoted"']:
            result = subprocess.run(["git", "check-ref-format", "--allow-onelevel", ref],
                                    text=True, capture_output=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stderr)

    def test_check_step_treats_an_adversarial_ref_as_data(self):
        (self.root / "CHANGELOG.md").write_text("## [0.3.0]\nNotes.\n")
        ref = 'v";touch${IFS}injected;#'
        script = step_script("Check the tag and extract the release notes")
        script = script.replace("${{ github.ref_name }}", ref).replace("${{ github.event_name }}", "push")
        script = script.replace("${{ steps.version.outputs.version }}", "0.3.0")
        result = self.run_bash(script, step_environment("Check the tag and extract the release notes", ref))
        self.assertFalse((self.root / "injected").exists(), "ref text executed as Bash source")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(ref, result.stderr)

    def test_check_step_dispatch_and_tag_positive_controls(self):
        (self.root / "CHANGELOG.md").write_text("## [0.3.0]\nNotes.\n")
        for event, ref in [("push", "v0.3.0"), ("workflow_dispatch", "arbitrary-branch")]:
            script = step_script("Check the tag and extract the release notes")
            script = script.replace("${{ github.ref_name }}", ref).replace("${{ github.event_name }}", event)
            script = script.replace("${{ steps.version.outputs.version }}", "0.3.0")
            result = self.run_bash(script, step_environment("Check the tag and extract the release notes", ref, event))
            self.assertEqual(0, result.returncode, result.stderr)

    def test_release_step_passes_one_literal_tag_argument(self):
        (self.root / "gh").write_text('#!/usr/bin/env bash\nprintf "%s\\n" "$@" > arguments.txt\n')
        (self.root / "gh").chmod(0o755)
        (self.root / "artifacts" / "packages").mkdir(parents=True)
        (self.root / "artifacts" / "packages" / "sample.nupkg").touch()
        (self.root / "release-notes.md").write_text("Notes.")
        ref = 'v$(touch${IFS}injected)-"quoted"'
        script = step_script("Create release").replace("${{ github.ref_name }}", ref)
        environment = step_environment("Create release", ref)
        environment["PATH"] = str(self.root) + os.pathsep + os.environ["PATH"]
        result = self.run_bash(script, environment)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse((self.root / "injected").exists(), "ref command substitution executed")
        args = (self.root / "arguments.txt").read_text().splitlines()
        self.assertEqual(["release", "create", ref], args[:3])
        self.assertIn("--verify-tag", args)


class LinuxPreflightGuards(unittest.TestCase):
    """Bash command probes on either hosted OS; these are not Windows PowerShell evidence."""
    def run_preflight(self, missing="", failing="", sdk_suffix="", selected_sdk=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sdk = json.loads((ROOT / "global.json").read_text(encoding="utf-8-sig"))["sdk"]["version"]
            (root / "global.json").write_text(json.dumps({"sdk": {"version": sdk, "rollForward": "latestPatch"}}))
            shutil.copy2(SCRIPTS / "preflight-linux.sh", root)
            env = dict(os.environ)
            env.pop("APTHERMO_NO_CUDA", None)
            env.update({"MISSING_TOOL": missing, "FAILING_TOOL": failing, "CONTROLLED_SDK": (selected_sdk or sdk) + sdk_suffix})
            script = r'''
controlled_exit() { if [[ "$1" == "$FAILING_TOOL" ]]; then return 7; fi; if [[ "$1" == "$MISSING_TOOL" ]]; then return 127; fi; return 0; }
git() { printf '%s\n' 'git version controlled'; controlled_exit git; }
dotnet() { if [[ "$1" == --list-sdks ]]; then printf '%s [controlled]\n' "$CONTROLLED_SDK"; else printf '%s\n' "$CONTROLLED_SDK"; fi; controlled_exit dotnet; }
nvidia-smi() { printf '%s\n' 'controlled NVIDIA probe'; controlled_exit nvidia-smi; }
command() { if [[ "$1" == '-v' && "${2:-}" == "$MISSING_TOOL" ]]; then return 1; fi; builtin command "$@"; }
source ./preflight-linux.sh
'''
            return subprocess.run([bash_path(), "-c", script], cwd=root, env=env,
                                  text=True, capture_output=True, timeout=30)

    def test_valid_host_prerequisites_pass(self):
        result = self.run_preflight()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("all checks passed", result.stdout)

    def test_each_missing_command_refuses(self):
        for name in ["git", "dotnet", "nvidia-smi"]:
            with self.subTest(name=name):
                result = self.run_preflight(missing=name)
                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn(name, result.stdout)

    def test_each_failed_native_command_refuses(self):
        for name in ["git", "dotnet", "nvidia-smi"]:
            with self.subTest(name=name):
                result = self.run_preflight(failing=name)
                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn(name, result.stdout)

    def test_sdk_resolver_output_must_be_one_version_token(self):
        self.assertNotEqual(0, self.run_preflight(sdk_suffix=" invalid").returncode)

    def test_patch_roll_forward_uses_the_selected_sdk(self):
        result = self.run_preflight(selected_sdk="10.0.113")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_resolver_refusal_fails_even_with_version_output(self):
        self.assertNotEqual(0, self.run_preflight(failing="dotnet").returncode)


if os.name == "nt":
    class WindowsPreflightGuards(unittest.TestCase):
        """Windows PowerShell 5.1 with isolated .cmd tools; never calls a real GPU."""
        def run_preflight(self, missing=None, failing=None, sdk_suffix="", selected_sdk=None, multiple_git=False):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                tools = root / "bin"
                tools.mkdir()
                sdk = json.loads((ROOT / "global.json").read_text(encoding="utf-8-sig"))["sdk"]["version"]
                (root / "global.json").write_text(json.dumps({"sdk": {"version": sdk, "rollForward": "latestPatch"}}))
                for name, output in [("git", "git version controlled"), ("dotnet", (selected_sdk or sdk) + sdk_suffix),
                                     ("nvidia-smi", "controlled NVIDIA probe")]:
                    if name != missing:
                        lines = "@echo off\n"
                        if name == "dotnet":
                            lines += 'if "%~1"=="--list-sdks" (echo ' + output + ' [controlled]) else (echo ' + output + ')\n'
                        else:
                            lines += "echo " + output + "\n"
                        (tools / (name + ".cmd")).write_text(lines + "exit /b " + ("7" if name == failing else "0") + "\n")
                env = dict(os.environ)
                env["PATH"] = str(tools)
                if multiple_git:
                    second = root / "second"
                    second.mkdir()
                    (second / "git.cmd").write_text("@echo off\necho secondary git\nexit /b 7\n")
                    env["PATH"] += os.pathsep + str(second)
                env.pop("APTHERMO_NO_CUDA", None)
                powershell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
                command = "$global:LASTEXITCODE=0; & '" + str(SCRIPTS / "preflight-windows.ps1").replace("'", "''") + "'"
                return subprocess.run([str(powershell), "-NoProfile", "-NonInteractive", "-Command", command],
                                      cwd=root, env=env, text=True, capture_output=True, timeout=30)

        def test_valid_host_prerequisites_pass(self):
            result = self.run_preflight()
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("all checks passed", result.stdout)

        def test_missing_nvidia_smi_cannot_use_stale_success(self):
            result = self.run_preflight(missing="nvidia-smi")
            self.assertNotEqual(0, result.returncode)
            self.assertIn("nvidia-smi not found", result.stdout)
            self.assertNotIn("all checks passed", result.stdout)

        def test_each_missing_or_failed_native_command_refuses(self):
            for name in ["git", "dotnet", "nvidia-smi"]:
                for mode in ["missing", "failing"]:
                    with self.subTest(name=name, mode=mode):
                        result = self.run_preflight(**{mode: name})
                        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                        self.assertIn(name, result.stdout)

        def test_sdk_resolver_output_must_be_one_version_token(self):
            self.assertNotEqual(0, self.run_preflight(sdk_suffix=" invalid").returncode)

        def test_patch_roll_forward_uses_the_selected_sdk(self):
            result = self.run_preflight(selected_sdk="10.0.113")
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)

        def test_resolver_refusal_fails_even_with_version_output(self):
            self.assertNotEqual(0, self.run_preflight(failing="dotnet").returncode)

        def test_multiple_git_paths_select_only_the_first_application(self):
            result = self.run_preflight(multiple_git=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertNotIn("secondary git", result.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
