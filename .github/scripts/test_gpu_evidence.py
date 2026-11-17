#!/usr/bin/env python3
"""Host-only synthetic TRX controls for the .NET GPU evidence verifier."""
import json
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NODE = ROOT / 'tests/GpuEvidence'
DLL = NODE / 'bin/Release/net10.0/APThermo.GpuEvidence.dll'
SWEEP = ['APThermo.Execution.Tests.CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic', 'APThermo.Execution.Tests.CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio']
HARDWARE = {'scope': 'hardware', 'actualCuda': True, 'device': 'controlled', 'scenarioExecuted': True, 'refusalVerified': False}
DRIVER = {'scope': 'driver-only', 'actualCuda': False, 'device': None, 'scenarioExecuted': True, 'refusalVerified': False}


class EvidenceControls(unittest.TestCase):
    def setUp(self):
        (NODE / 'obj').mkdir(exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=NODE / 'obj')
        self.root = Path(self.directory.name)
        self.addCleanup(self.directory.cleanup)
        self.cases = [dict(Id=str(i), Name=name, Fqn=name, Categories=['Cuda'], Scope=scope) for i, (name, scope) in enumerate([('Hardware', 'hardware'), ('Driver', 'driver-only'), (SWEEP[0], 'hardware'), (SWEEP[1], 'hardware')], 1)]
        self.outputs = [HARDWARE.copy(), DRIVER.copy(), HARDWARE.copy(), HARDWARE.copy()]

    def report(self, cases, filename):
        report = ET.Element('TestRun', xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        summary = ET.SubElement(report, 'ResultSummary', outcome='Completed')
        ET.SubElement(summary, 'Counters', total=str(len(cases)), executed=str(len(cases)), passed=str(len(cases)), failed='0', error='0', aborted='0')
        results = ET.SubElement(report, 'Results')
        for index, (case, output) in enumerate(cases):
            item = ET.SubElement(results, 'UnitTestResult', executionId=str(index), testId=case['Id'], testName=case['Name'], outcome='Passed')
            if output is not None:
                markers = output if isinstance(output, list) else [output]
                ET.SubElement(ET.SubElement(item, 'Output'), 'StdOut').text = '\n'.join('CUDA_EVIDENCE:' + json.dumps(marker) for marker in markers)
        directory = self.root / 'tests/Project/TestResults'
        directory.mkdir(parents=True, exist_ok=True)
        ET.ElementTree(report).write(directory / filename, encoding='utf-8', xml_declaration=True)

    def run_gate(self, remainder=None, sweep=None):
        (self.root / 'inventory.json').write_text(json.dumps(self.cases), encoding='utf-8')
        pairs = list(zip(self.cases, self.outputs))
        self.report(pairs[:2] if remainder is None else remainder, 'remainder.trx')
        self.report(pairs[2:] if sweep is None else sweep, 'sweep.trx')
        return subprocess.run(['dotnet', str(DLL), 'verify', 'inventory.json', 'remainder.trx', 'sweep.trx'], cwd=self.root, capture_output=True, text=True, timeout=30)

    def test_valid_exact_population_and_both_scopes(self):
        result = self.run_gate()
        self.assertEqual(0, result.returncode, result.stderr)

    def test_empty_missing_duplicate_extra_and_mismatched_rows_fail(self):
        pairs = list(zip(self.cases, self.outputs))
        changed = dict(self.cases[0], Id='unexpected')
        for rows in [[], pairs[1:2], pairs[:2] + pairs[:1], pairs[:2] + [(changed, HARDWARE)], [(changed, HARDWARE), pairs[1]]]:
            with self.subTest(rows=rows):
                self.assertNotEqual(0, self.run_gate(remainder=rows).returncode)

    def test_missing_refusal_and_invalid_driver_markers_fail(self):
        pairs = list(zip(self.cases, self.outputs))
        missing_refusal = dict(HARDWARE)
        del missing_refusal['refusalVerified']
        missing_scenario = dict(HARDWARE)
        del missing_scenario['scenarioExecuted']
        for rows in [[(self.cases[0], missing_refusal), pairs[1]], [(self.cases[0], missing_scenario), pairs[1]], [(self.cases[0], None), pairs[1]], [(self.cases[0], [HARDWARE, HARDWARE]), pairs[1]], [(self.cases[0], dict(HARDWARE, actualCuda=False, refusalVerified=True)), pairs[1]], [(self.cases[0], dict(HARDWARE, refusalVerified=True)), pairs[1]], [(self.cases[0], dict(HARDWARE, scenarioExecuted=None)), pairs[1]], [(self.cases[0], dict(HARDWARE, scenarioExecuted=False)), pairs[1]], [(self.cases[0], dict(HARDWARE, scenarioExecuted="yes")), pairs[1]], [(self.cases[0], dict(HARDWARE, device=42)), pairs[1]], [pairs[0], (self.cases[1], dict(DRIVER, scenarioExecuted=False))], [pairs[0], (self.cases[1], dict(DRIVER, refusalVerified=True))], [pairs[0], (self.cases[1], dict(DRIVER, actualCuda=True))]]:
            with self.subTest(rows=rows):
                self.assertNotEqual(0, self.run_gate(remainder=rows).returncode)

    def test_empty_individual_report_is_neutral_and_summary_failures_are_refused(self):
        self.assertEqual(0, self.run_gate().returncode)
        self.report([], 'empty.trx')
        report = self.root / 'tests/Project/TestResults/empty.trx'
        report.rename(self.root / 'tests/Project/TestResults/another.trx')
        other = self.root / 'tests/Another/TestResults'
        other.mkdir(parents=True)
        (self.root / 'tests/Project/TestResults/another.trx').rename(other / 'remainder.trx')
        command = ['dotnet', str(DLL), 'verify', 'inventory.json', 'remainder.trx', 'sweep.trx']
        self.assertEqual(0, subprocess.run(command, cwd=self.root, capture_output=True, text=True, timeout=30).returncode)
        path = self.root / 'tests/Project/TestResults/remainder.trx'
        original = path.read_text(encoding='utf-8')
        for before, after in [('outcome="Completed"', 'outcome="Aborted"'), ('outcome="Completed"', 'outcome="Error"'), ('passed="2"', 'passed="1"'), ('failed="0"', 'failed="1"'), ('error="0"', 'error="1"'), ('aborted="0"', 'aborted="1"')]:
            with self.subTest(change=after):
                self.assertIn(before, original)
                path.write_text(original.replace(before, after, 1), encoding='utf-8')
                self.assertNotEqual(0, subprocess.run(command, cwd=self.root, capture_output=True, text=True, timeout=30).returncode)

    def test_missing_and_truncated_report_fail(self):
        self.assertEqual(0, self.run_gate().returncode)
        report = self.root / 'tests/Project/TestResults/remainder.trx'
        for content in [None, '<TestRun', '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Counters total="2" executed="2" /></TestRun>']:
            with self.subTest(content=content):
                if content is None:
                    report.unlink()
                else:
                    report.write_text(content, encoding='utf-8')
                result = subprocess.run(['dotnet', str(DLL), 'verify', 'inventory.json', 'remainder.trx', 'sweep.trx'], cwd=self.root, capture_output=True, text=True, timeout=30)
                self.assertNotEqual(0, result.returncode)


if __name__ == '__main__':
    unittest.main(verbosity=2)
