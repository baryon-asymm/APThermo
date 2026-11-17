"""Real MSBuild controls for the solution-only test-build barrier."""

from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
HOOK = ROOT / "Directory.Solution.targets"
TIMEOUT = 60
PROJECT_IDS = ("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222")
ENVIRONMENT = {**os.environ, "MSBUILDDISABLENODEREUSE": "1"}


def selected_sdk(path):
    result = subprocess.run(["dotnet", "--version"], cwd=path, capture_output=True,
                            text=True, encoding="utf-8", errors="replace", timeout=TIMEOUT,
                            check=True, env=ENVIRONMENT)
    return result.stdout.strip()


def solution_text():
    rows = ["Microsoft Visual Studio Solution File, Format Version 12.00"]
    for name, guid in zip(("A", "B"), PROJECT_IDS):
        rows.extend((
            f'Project("{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}") = "{name}", "{name}.csproj", "{{{guid}}}"',
            "EndProject",
        ))
    rows.extend(("Global", "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
                 "\t\tDebug|Any CPU = Debug|Any CPU", "\tEndGlobalSection",
                 "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"))
    for guid in PROJECT_IDS:
        rows.extend((f"\t\t{{{guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                     f"\t\t{{{guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU"))
    rows.extend(("\tEndGlobalSection", "EndGlobal", ""))
    return "\n".join(rows)


def project_text(name):
    other = "B" if name == "A" else "A"
    return f'''<Project>
  <Target Name="Build">
    <Error Condition="'$(FailBuild)' == 'true'" Text="intentional build failure" />
    <WriteLinesToFile File="$(MSBuildThisFileDirectory){name}.built" Lines="built" Overwrite="true" />
  </Target>
  <Target Name="VSTest">
    <Error Condition="'$(VSTestNoBuild)' != 'true' and (!Exists('$(MSBuildThisFileDirectory){name}.built') or !Exists('$(MSBuildThisFileDirectory){other}.built'))" Text="VSTest started before all builds" />
    <WriteLinesToFile File="$(MSBuildThisFileDirectory){name}.tested" Lines="tested" Overwrite="true" />
  </Target>
</Project>
'''


class SolutionOrderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repository_sdk = selected_sdk(ROOT)
        print(f"Repository-selected SDK: {cls.repository_sdk}", flush=True)

    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory(prefix="apt-solution-order-")
        self.addCleanup(self.workspace.cleanup)
        self.path = Path(self.workspace.name)
        shutil.copyfile(ROOT / "global.json", self.path / "global.json")
        self.assertEqual(selected_sdk(self.path), self.repository_sdk)
        (self.path / "Fixture.sln").write_text(solution_text(), encoding="utf-8")
        for name in ("A", "B"):
            (self.path / f"{name}.csproj").write_text(project_text(name), encoding="utf-8")

    def invoke(self, args, hook=None):
        if hook:
            (self.path / "Directory.Solution.targets").write_text(hook, encoding="utf-8")
        return subprocess.run(["dotnet", *args], cwd=self.path, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=TIMEOUT,
                              check=False, env=ENVIRONMENT)

    def current_hook(self):
        return HOOK.read_text(encoding="utf-8") if HOOK.exists() else ""

    def assert_passed(self, result):
        self.assertEqual(result.returncode, 0, result.stdout + "\n" + result.stderr)

    def test_all_builds_precede_each_test(self):
        result = self.invoke(["test", "Fixture.sln", "--no-restore"], self.current_hook())
        self.assert_passed(result)
        for name in ("A", "B"):
            self.assertTrue((self.path / f"{name}.built").is_file())
            self.assertTrue((self.path / f"{name}.tested").is_file())

    def test_build_failure_stops_all_tests(self):
        result = self.invoke(["test", "Fixture.sln", "--no-restore", "-p:FailBuild=true"], self.current_hook())
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("intentional build failure", result.stdout + result.stderr)
        for name in ("A", "B"):
            self.assertFalse((self.path / f"{name}.tested").exists())

    def test_explicit_no_build_executes_tests_without_build(self):
        result = self.invoke(["test", "Fixture.sln", "--no-build", "--no-restore"], self.current_hook())
        self.assert_passed(result)
        for name in ("A", "B"):
            self.assertFalse((self.path / f"{name}.built").exists())
            self.assertTrue((self.path / f"{name}.tested").is_file())

    def test_ordinary_build_does_not_run_tests(self):
        result = self.invoke(["build", "Fixture.sln", "--no-restore"], self.current_hook())
        self.assert_passed(result)
        for name in ("A", "B"):
            self.assertTrue((self.path / f"{name}.built").is_file())
            self.assertFalse((self.path / f"{name}.tested").exists())

    def test_missing_dependency_mutation_does_not_preserve_order(self):
        source = self.current_hook()
        self.assertIn('DependsOnTargets="Build"', source)
        mutant = source.replace('DependsOnTargets="Build"', '', 1)
        result = self.invoke(["test", "Fixture.sln", "--no-restore"], mutant)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("VSTest started before all builds", result.stdout + result.stderr)

    def test_missing_condition_mutation_violates_no_build(self):
        source = self.current_hook()
        condition = 'Condition="\'$(VSTestNoBuild)\' != \'true\'"'
        self.assertIn(condition, source)
        result = self.invoke(["test", "Fixture.sln", "--no-build", "--no-restore"],
                             source.replace(condition, '', 1))
        self.assert_passed(result)
        self.assertTrue(any((self.path / f"{name}.built").exists() for name in ("A", "B")))


if __name__ == "__main__":
    unittest.main(verbosity=2)
