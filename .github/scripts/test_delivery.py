#!/usr/bin/env python3
"""Controlled local guards; actual GitHub runner proof is separate delivery evidence."""
import json
import os
from pathlib import Path
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
