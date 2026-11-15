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
        if one_value(job, "GPU_TEST_TRX", 6) != "gpu-${{ github.run_id }}-${{ github.run_attempt }}.trx":
            problems.append("TRX identity is not run/attempt-specific")
        checkout = gpu_step(job, "Checkout")
        if one_value(checkout, "persist-credentials", 10) != "false" or one_value(checkout, "clean", 10) != "true":
            problems.append("fresh credential-free checkout changed")
        if one_value(gpu_step(job, "Build"), "shell", 8) != shell:
            problems.append("explicit GPU build shell changed")
        step = gpu_step(job, "CUDA tests")
        if one_value(step, "shell", 8) != shell:
            problems.append("explicit GPU shell changed")
        try:
            arguments = shlex.split(one_value(step, "run", 8))
        except ValueError:
            arguments = []
        expected = ["dotnet", "test", "APThermo.sln", "--configuration", "Release", "--no-build",
                    "-m:1", "--filter", "Category=Cuda|Category=BitSnapshot",
                    "--logger", "trx;LogFileName=" + variable]
        if arguments != expected:
            problems.append("GPU command lost serialization, selection or fresh logger")
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
                     "tests/Execution.Tests/Throughput*.actual.txt"]:
            problems.append("per-project TRX or throughput evidence changed")
    if "  pull_request:" in text or "    branches:" in text:
        for problems in violations.values():
            problems.append("self-hosted release admits an unapproved event")
    return violations


class GpuReleaseGuards(unittest.TestCase):
    def test_both_gpu_jobs_obey_the_release_contract(self):
        for name, problems in gpu_release_violations(RELEASE.read_text(encoding="utf-8")).items():
            with self.subTest(job=name):
                self.assertEqual([], problems)

    def test_each_gpu_job_rejects_command_shell_dependency_and_artifact_drift(self):
        text = RELEASE.read_text(encoding="utf-8")
        self.assertEqual({"cuda-windows": [], "cuda-linux": []}, gpu_release_violations(text))
        for name in ["cuda-windows", "cuda-linux"]:
            original = gpu_job(text, name)
            changes = {
                "serialization removed": (" -m:1", ""),
                "serialization widened": (" -m:1", " -m:2"),
                "selection narrowed": ("Category=Cuda|Category=BitSnapshot", "Category=Cuda"),
                "build shell implicit": ("        shell: powershell" if name == "cuda-windows" else "        shell: bash", ""),
                "test shell implicit": ("      - name: CUDA tests\n        shell: powershell" if name == "cuda-windows" else "      - name: CUDA tests\n        shell: bash", "      - name: CUDA tests"),
                "dependency lost": ("    needs: matrix" if name == "cuda-windows" else "    needs: [matrix, cuda-windows]", "    needs: check"),
                "timeout raised": ("    timeout-minutes: 180", "    timeout-minutes: 181"),
                "stale TRX name": ("gpu-${{ github.run_id }}-${{ github.run_attempt }}.trx", "gpu.trx"),
                "checkout not fresh": ("          clean: true", "          clean: false"),
                "artifact collides": ("gpu-tests-windows-" if name == "cuda-windows" else "gpu-tests-linux-", "gpu-tests-"),
                "upload conditional": ("        if: always()", "        if: success()"),
                "missing evidence ignored": ("          if-no-files-found: error", "          if-no-files-found: ignore"),
                "project reports dropped": ("            tests/**/TestResults/${{ env.GPU_TEST_TRX }}", "            tests/Execution.Tests/TestResults/${{ env.GPU_TEST_TRX }}"),
            }
            for mutation, (before, after) in changes.items():
                with self.subTest(job=name, mutation=mutation):
                    self.assertIn(before, original)
                    changed = text.replace(original, original.replace(before, after, 1), 1)
                    problems = gpu_release_violations(changed)
                    self.assertTrue(problems[name], "mutation was not refused")
                    other = "cuda-linux" if name == "cuda-windows" else "cuda-windows"
                    self.assertEqual([], problems[other], "the guard confused the two GPU jobs")

    def test_available_shells_receive_the_exact_arguments(self):
        text = RELEASE.read_text(encoding="utf-8")
        jobs = ["cuda-linux"] + (["cuda-windows"] if os.name == "nt" else [])
        expected = ["test", "APThermo.sln", "--configuration", "Release", "--no-build", "-m:1",
                    "--filter", "Category=Cuda|Category=BitSnapshot", "--logger", "trx;LogFileName=gpu-123-4.trx"]
        for name in jobs:
            with self.subTest(job=name), tempfile.TemporaryDirectory() as directory:
                command = one_value(gpu_step(gpu_job(text, name), "CUDA tests"), "run", 8)
                environment = dict(os.environ, GPU_TEST_TRX="gpu-123-4.trx")
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
                    self.assertEqual(0, result.returncode, result.stderr)
                    actual = [base64.b64decode(line).decode("utf-8") for line in result.stdout.splitlines()]
                else:
                    script = 'dotnet() { printf \'%s\\0\' "$@" > argv.bin; }\n' + command
                    result = subprocess.run([bash_path(), "-c", script], cwd=directory, env=environment,
                                            text=True, capture_output=True, timeout=30)
                    self.assertEqual(0, result.returncode, result.stderr)
                    actual = (Path(directory) / "argv.bin").read_bytes().decode().split("\0")[:-1]
                self.assertEqual(expected, actual)

    def test_release_refuses_pr_and_branch_push_execution(self):
        text = RELEASE.read_text(encoding="utf-8")
        self.assertEqual({"cuda-windows": [], "cuda-linux": []}, gpu_release_violations(text))
        for inserted in ["  pull_request:\n", "  push:\n    branches: ['**']\n"]:
            with self.subTest(event=inserted):
                self.assertTrue(all(gpu_release_violations(text.replace("on:\n", "on:\n" + inserted, 1)).values()))


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
